using System.Data;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services;
using Kanban.Api.Services.Notifications;
using Kanban.Api.Services.Projects;
using Kanban.Api.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Cards;

public sealed class CardService : ICardService
{
    private const int PositionGap = 1000;
    private const int DefaultBoardCardsPageSize = 50;
    private const int MaxBoardCardsPageSize = 50;
    private const int DefaultSearchCardsPageSize = 25;
    private const int MaxSearchCardsPageSize = 25;
    private const int DefaultArchivedCardsPageSize = 25;
    private const int MaxArchivedCardsPageSize = 25;

    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ISubscriptionService? _subscriptionService;

    public CardService(ApplicationDbContext dbContext, INotificationService notificationService)
        : this(dbContext, notificationService, null)
    {
    }

    public CardService(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        ISubscriptionService? subscriptionService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _subscriptionService = subscriptionService;
    }

    public async Task<PaginatedResponse<Card>> ListByBoardAsync(Guid boardId, Guid userId, int page, int pageSize)
    {
        var boardExists = await _dbContext.Boards
            .AsNoTracking()
            .AnyAsync(x => x.Id == boardId);

        if (!boardExists)
        {
            throw new KeyNotFoundException("Board not found.");
        }

        var boardProjectId = await _dbContext.Boards
            .AsNoTracking()
            .Where(x => x.Id == boardId)
            .Select(x => x.ProjectId)
            .FirstAsync();

        var hasAccess = await CheckProjectAccessAsync(boardProjectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize <= 0
            ? DefaultBoardCardsPageSize
            : Math.Min(pageSize, MaxBoardCardsPageSize);

        var query = _dbContext.Cards
            .AsNoTracking()
            .Where(x => x.Column.BoardId == boardId);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(x => x.Column.Position)
            .ThenBy(x => x.Position)
            .ThenBy(x => x.Id)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync();

        return new PaginatedResponse<Card>(items, effectivePage, effectivePageSize, totalCount);
    }

    public async Task<PaginatedResponse<Card>> SearchAsync(Guid projectId, Guid userId, string query, int page, int pageSize)
    {
        var projectExists = await _dbContext.Projects
            .AsNoTracking()
            .AnyAsync(x => x.Id == projectId);

        if (!projectExists)
        {
            throw new KeyNotFoundException("Project not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(projectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize <= 0
            ? DefaultSearchCardsPageSize
            : Math.Min(pageSize, MaxSearchCardsPageSize);

        if (string.IsNullOrWhiteSpace(query))
        {
            return new PaginatedResponse<Card>(Array.Empty<Card>(), effectivePage, effectivePageSize, 0);
        }

        var normalizedQuery = query.Trim();

        var baseQuery = _dbContext.Cards
            .AsNoTracking()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .Where(x => x.Column.Board.ProjectId == projectId);

        IQueryable<Card> searchQuery;
        var providerName = _dbContext.Database.ProviderName;
        var isNpgsql = !string.IsNullOrWhiteSpace(providerName)
            && providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);

        if (isNpgsql)
        {
            var wildcardQuery = $"%{normalizedQuery}%";
            searchQuery = baseQuery.Where(x => EF.Functions.ILike(x.Title, wildcardQuery)
                || EF.Functions.ILike(x.Description ?? string.Empty, wildcardQuery));
        }
        else
        {
            var normalizedLower = normalizedQuery.ToLower();
            searchQuery = baseQuery.Where(x => (x.Title ?? string.Empty).ToLower().Contains(normalizedLower)
                || (x.Description ?? string.Empty).ToLower().Contains(normalizedLower));
        }

        var totalCount = await searchQuery.CountAsync();

        var items = await searchQuery
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Id)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync();

        return new PaginatedResponse<Card>(items, effectivePage, effectivePageSize, totalCount);
    }

    public async Task<List<Card>> FilterAsync(Guid boardId, Guid userId, FilterCriteria filters)
    {
        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == boardId);

        if (board is null)
        {
            throw new KeyNotFoundException("Board not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(board.ProjectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var tagIds = filters.TagIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        var userIds = filters.UserIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        var columnIds = filters.ColumnIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        var query = _dbContext.Cards
            .AsNoTracking()
            .Include(x => x.Column)
            .Where(x => x.Column.BoardId == boardId)
            .AsQueryable();

        if (tagIds.Length > 0)
        {
            query = query.Where(x => x.CardTags.Any(cardTag => tagIds.Contains(cardTag.TagId)));
        }

        if (userIds.Length > 0)
        {
            query = query.Where(x => x.Assignments.Any(assignment => userIds.Contains(assignment.UserId)));
        }

        if (columnIds.Length > 0)
        {
            query = query.Where(x => columnIds.Contains(x.ColumnId));
        }

        return await query
            .OrderBy(x => x.Column.Position)
            .ThenBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<Card> CreateAsync(Guid columnId, Guid userId, CreateCardDto data)
    {
        if (string.IsNullOrWhiteSpace(data.Title))
        {
            throw new InvalidOperationException("Card title is required.");
        }

        var column = await _dbContext.Columns
            .AsNoTracking()
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId);

        if (column is null)
        {
            throw new KeyNotFoundException("Column not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var maxPosition = await _dbContext.Cards
            .Where(x => x.ColumnId == columnId)
            .Select(x => (int?)x.Position)
            .MaxAsync();

        var now = DateTime.UtcNow;
        var card = new Card
        {
            Id = Guid.NewGuid(),
            ColumnId = columnId,
            Title = data.Title.Trim(),
            Description = NormalizeDescription(data.Description),
            Position = (maxPosition ?? 0) + PositionGap,
            PlannedDurationHours = data.PlannedDurationHours,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId
        };

        _dbContext.Cards.Add(card);
        await _dbContext.SaveChangesAsync();

        await NotifySubscribersAsync(
            EntityType.Column,
            columnId,
            userId,
            NotificationType.CardCreated,
            $"Card created: {card.Title}",
            $"{await GetActorLabelAsync(userId)} created card '{card.Title}' in column '{column.Name}'.",
            entityType: "card",
            entityId: card.Id,
            createdBy: userId);

        return card;
    }

    public async Task<Card> GetByIdAsync(Guid cardId, Guid userId)
    {
        var card = await _dbContext.Cards
            .AsNoTracking()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .Include(x => x.CardTags)
                .ThenInclude(x => x.Tag)
            .Include(x => x.Assignments)
                .ThenInclude(x => x.User)
            .Include(x => x.Attachments)
            .Include(x => x.Subtasks)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Card not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        return card;
    }

    public async Task<Card> UpdateAsync(Guid cardId, Guid userId, UpdateCardDto data)
    {
        if (string.IsNullOrWhiteSpace(data.Title))
        {
            throw new InvalidOperationException("Card title is required.");
        }

        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Card not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        if (card.Version != data.Version)
        {
            throw new ConflictException("Card has been modified. Please refresh and try again.");
        }

        card.Title = data.Title.Trim();
        card.Description = NormalizeDescription(data.Description);
        card.PlannedDurationHours = data.PlannedDurationHours;
        card.Version += 1;
        card.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Card has been modified. Please refresh and try again.");
        }

        await NotifySubscribersAsync(
            EntityType.Card,
            card.Id,
            userId,
            NotificationType.CardUpdated,
            $"Card updated: {card.Title}",
            $"{await GetActorLabelAsync(userId)} updated card '{card.Title}'.",
            entityType: "card",
            entityId: card.Id,
            createdBy: userId);

        return card;
    }

    public async Task<Card> MoveAsync(Guid cardId, Guid userId, MoveCardDto data)
    {
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;

        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Card not found.");
        }

        var targetColumn = await _dbContext.Columns
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == data.ColumnId);

        if (targetColumn is null)
        {
            throw new KeyNotFoundException("Column not found.");
        }

        if (card.Column.Board.ProjectId != targetColumn.Board.ProjectId)
        {
            throw new InvalidOperationException("Card can only be moved within the same project.");
        }

        var sourceColumnId = card.ColumnId;
        var sourceColumnName = card.Column.Name;

        var hasAccess = await CheckProjectAccessAsync(targetColumn.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var targetCards = await _dbContext.Cards
            .Where(x => x.ColumnId == targetColumn.Id && x.Id != card.Id)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var insertionIndex = Math.Clamp(data.Position, 0, targetCards.Count);
        var before = insertionIndex > 0 ? targetCards[insertionIndex - 1] : null;
        var after = insertionIndex < targetCards.Count ? targetCards[insertionIndex] : null;

        var now = DateTime.UtcNow;
        var requiresRenumber = false;
        var newPosition = PositionGap;

        if (before is not null && after is not null)
        {
            var gap = after.Position - before.Position;
            requiresRenumber = gap < 2;
            newPosition = (before.Position + after.Position) / 2;
        }
        else if (before is null && after is not null)
        {
            newPosition = after.Position - PositionGap;
            requiresRenumber = targetCards.Any(x => x.Position == newPosition);
        }
        else if (before is not null)
        {
            newPosition = before.Position + PositionGap;
            requiresRenumber = targetCards.Any(x => x.Position == newPosition);
        }

        if (!requiresRenumber)
        {
            card.ColumnId = targetColumn.Id;
            card.Position = newPosition;
            card.UpdatedAt = now;

            await _dbContext.SaveChangesAsync();

            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }

            var actorLabel = await GetActorLabelAsync(userId);

            await NotifySubscribersAsync(
                EntityType.Card,
                card.Id,
                userId,
                NotificationType.CardMoved,
                $"Card moved: {card.Title}",
                $"{actorLabel} moved card '{card.Title}' from '{sourceColumnName}' to '{targetColumn.Name}'.",
                entityType: "card",
                entityId: card.Id,
                createdBy: userId);

            if (targetColumn.Id != sourceColumnId)
            {
                await NotifySubscribersAsync(
                    EntityType.Column,
                    targetColumn.Id,
                    userId,
                    NotificationType.CardMoved,
                    $"Card moved into column: {card.Title}",
                    $"{actorLabel} moved card '{card.Title}' into column '{targetColumn.Name}'.",
                    entityType: "card",
                    entityId: card.Id,
                    createdBy: userId);
            }

            return card;
        }

        var reordered = BuildOrderedCards(targetCards, card, insertionIndex);

        for (var index = 0; index < reordered.Count; index++)
        {
            reordered[index].ColumnId = targetColumn.Id;
            reordered[index].Position = (index + 1) * PositionGap;
            reordered[index].UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync();

        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }

        var actor = await GetActorLabelAsync(userId);

        await NotifySubscribersAsync(
            EntityType.Card,
            card.Id,
            userId,
            NotificationType.CardMoved,
            $"Card moved: {card.Title}",
            $"{actor} moved card '{card.Title}' from '{sourceColumnName}' to '{targetColumn.Name}'.",
            entityType: "card",
            entityId: card.Id,
            createdBy: userId);

        if (targetColumn.Id != sourceColumnId)
        {
            await NotifySubscribersAsync(
                EntityType.Column,
                targetColumn.Id,
                userId,
                NotificationType.CardMoved,
                $"Card moved into column: {card.Title}",
                $"{actor} moved card '{card.Title}' into column '{targetColumn.Name}'.",
                entityType: "card",
                entityId: card.Id,
                createdBy: userId);
        }

        return card;
    }

    public async Task AssignTagAsync(Guid cardId, Guid tagId, Guid userId)
    {
        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Card not found.");
        }

        var projectId = card.Column.Board.ProjectId;
        var hasAccess = await CheckProjectAccessAsync(projectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var tag = await _dbContext.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tagId);

        if (tag is null)
        {
            throw new KeyNotFoundException("Tag not found.");
        }

        if (tag.ProjectId != projectId)
        {
            throw new InvalidOperationException("Tag belongs to a different project.");
        }

        var exists = await _dbContext.CardTags
            .AnyAsync(x => x.CardId == cardId && x.TagId == tagId);

        if (exists)
        {
            return;
        }

        _dbContext.CardTags.Add(new CardTag
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            TagId = tagId,
            CreatedAt = DateTime.UtcNow
        });

        card.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    public async Task UnassignTagAsync(Guid cardId, Guid tagId, Guid userId)
    {
        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Card not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var cardTag = await _dbContext.CardTags
            .FirstOrDefaultAsync(x => x.CardId == cardId && x.TagId == tagId);

        if (cardTag is null)
        {
            return;
        }

        _dbContext.CardTags.Remove(cardTag);
        card.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    public async Task AssignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId)
    {
        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Card not found.");
        }

        var projectId = card.Column.Board.ProjectId;
        var hasAccess = await CheckProjectAccessAsync(projectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var assigneeIsProjectMember = await _dbContext.ProjectMembers
            .AnyAsync(x => x.ProjectId == projectId && x.UserId == assigneeUserId);

        if (!assigneeIsProjectMember)
        {
            throw new InvalidOperationException("Assigned user must be a project member.");
        }

        var exists = await _dbContext.CardAssignments
            .AnyAsync(x => x.CardId == cardId && x.UserId == assigneeUserId);

        if (exists)
        {
            return;
        }

        _dbContext.CardAssignments.Add(new CardAssignment
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = assigneeUserId,
            AssignedBy = userId,
            AssignedAt = DateTime.UtcNow
        });

        card.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        if (assigneeUserId == userId)
        {
            return;
        }

        var assignerDisplay = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.Email ?? x.UserName)
            .FirstOrDefaultAsync();

        var assignerLabel = string.IsNullOrWhiteSpace(assignerDisplay)
            ? "A user"
            : assignerDisplay;

        await _notificationService.CreateAsync(
            assigneeUserId,
            NotificationType.CardAssigned,
            $"Assigned: {card.Title}",
            $"{assignerLabel} assigned you to card '{card.Title}'.",
            entityType: "card",
            entityId: card.Id,
            createdBy: userId);
    }

    public async Task UnassignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId)
    {
        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Card not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var assignment = await _dbContext.CardAssignments
            .FirstOrDefaultAsync(x => x.CardId == cardId && x.UserId == assigneeUserId);

        if (assignment is null)
        {
            return;
        }

        _dbContext.CardAssignments.Remove(assignment);
        card.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<Subtask> CreateSubtaskAsync(Guid cardId, Guid userId, CreateSubtaskDto data)
    {
        if (string.IsNullOrWhiteSpace(data.Description))
        {
            throw new InvalidOperationException("Subtask description is required.");
        }

        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Card not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var maxPosition = await _dbContext.Subtasks
            .Where(x => x.CardId == cardId)
            .Select(x => (int?)x.Position)
            .MaxAsync();

        var now = DateTime.UtcNow;
        var subtask = new Subtask
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            Description = data.Description.Trim(),
            Completed = false,
            Position = (maxPosition ?? 0) + PositionGap,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Subtasks.Add(subtask);
        card.UpdatedAt = now;

        await _dbContext.SaveChangesAsync();
        return subtask;
    }

    public async Task<Subtask> UpdateSubtaskAsync(Guid subtaskId, Guid userId, UpdateSubtaskDto data)
    {
        if (data.Description is null && data.Completed is null)
        {
            throw new InvalidOperationException("At least one subtask field must be provided.");
        }

        if (data.Description is not null && string.IsNullOrWhiteSpace(data.Description))
        {
            throw new InvalidOperationException("Subtask description is required.");
        }

        var subtask = await _dbContext.Subtasks
            .Include(x => x.Card)
                .ThenInclude(x => x.Column)
                    .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == subtaskId);

        if (subtask is null)
        {
            throw new KeyNotFoundException("Subtask not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(subtask.Card.Column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        if (data.Description is not null)
        {
            subtask.Description = data.Description.Trim();
        }

        if (data.Completed.HasValue)
        {
            subtask.Completed = data.Completed.Value;
        }

        var now = DateTime.UtcNow;
        subtask.UpdatedAt = now;
        subtask.Card.UpdatedAt = now;

        await _dbContext.SaveChangesAsync();
        return subtask;
    }

    public async Task DeleteSubtaskAsync(Guid subtaskId, Guid userId)
    {
        var subtask = await _dbContext.Subtasks
            .Include(x => x.Card)
                .ThenInclude(x => x.Column)
                    .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == subtaskId);

        if (subtask is null)
        {
            throw new KeyNotFoundException("Subtask not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(subtask.Card.Column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        subtask.Card.UpdatedAt = DateTime.UtcNow;
        _dbContext.Subtasks.Remove(subtask);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<SubtaskCountsDto> GetSubtaskCountsAsync(Guid cardId, Guid userId)
    {
        var card = await _dbContext.Cards
            .AsNoTracking()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Card not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var total = await _dbContext.Subtasks
            .Where(x => x.CardId == cardId)
            .CountAsync();

        var completed = await _dbContext.Subtasks
            .Where(x => x.CardId == cardId && x.Completed)
            .CountAsync();

        return new SubtaskCountsDto(completed, total);
    }

    public async Task ArchiveAsync(Guid cardId, Guid userId)
    {
        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Card not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync()
            : null;

        var now = DateTime.UtcNow;
        card.DeletedAt = now;
        card.UpdatedAt = now;

        var attachments = await _dbContext.Attachments
            .Where(x => x.CardId == card.Id)
            .ToListAsync();

        var subtasks = await _dbContext.Subtasks
            .Where(x => x.CardId == card.Id)
            .ToListAsync();

        foreach (var attachment in attachments)
        {
            attachment.DeletedAt = now;
        }

        foreach (var subtask in subtasks)
        {
            subtask.DeletedAt = now;
            subtask.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync();

        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }

        await NotifySubscribersAsync(
            EntityType.Card,
            card.Id,
            userId,
            NotificationType.CardDeleted,
            $"Card deleted: {card.Title}",
            $"{await GetActorLabelAsync(userId)} deleted card '{card.Title}'.",
            entityType: "card",
            entityId: card.Id,
            createdBy: userId);
    }

    public async Task RestoreAsync(Guid cardId, Guid userId)
    {
        var card = await _dbContext.Cards
            .IgnoreQueryFilters()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new KeyNotFoundException("Card not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync()
            : null;

        var now = DateTime.UtcNow;
        card.DeletedAt = null;
        card.UpdatedAt = now;

        var attachments = await _dbContext.Attachments
            .IgnoreQueryFilters()
            .Where(x => x.CardId == card.Id)
            .ToListAsync();

        var subtasks = await _dbContext.Subtasks
            .IgnoreQueryFilters()
            .Where(x => x.CardId == card.Id)
            .ToListAsync();

        foreach (var attachment in attachments)
        {
            attachment.DeletedAt = null;
        }

        foreach (var subtask in subtasks)
        {
            subtask.DeletedAt = null;
            subtask.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync();

        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }
    }

    public async Task<PaginatedResponse<Card>> ListArchivedAsync(Guid userId, int page, int pageSize)
    {
        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize <= 0
            ? DefaultArchivedCardsPageSize
            : Math.Min(pageSize, MaxArchivedCardsPageSize);

        var query =
            from card in _dbContext.Cards.IgnoreQueryFilters().AsNoTracking()
            join column in _dbContext.Columns.IgnoreQueryFilters().AsNoTracking()
                on card.ColumnId equals column.Id
            join board in _dbContext.Boards.IgnoreQueryFilters().AsNoTracking()
                on column.BoardId equals board.Id
            where card.DeletedAt != null
            where _dbContext.ProjectMembers.Any(pm =>
                pm.ProjectId == board.ProjectId
                && pm.UserId == userId
                && pm.Role <= ProjectRole.Viewer)
            select card;

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.DeletedAt)
            .ThenBy(x => x.Id)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync();

        return new PaginatedResponse<Card>(items, effectivePage, effectivePageSize, totalCount);
    }

    private async Task<bool> CheckProjectAccessAsync(Guid projectId, Guid userId, ProjectRole minimumRole)
    {
        var role = await _dbContext.ProjectMembers
            .Where(x => x.ProjectId == projectId && x.UserId == userId)
            .Select(x => (ProjectRole?)x.Role)
            .FirstOrDefaultAsync();

        if (role is null)
        {
            return false;
        }

        return HasRequiredRole(role.Value, minimumRole);
    }

    private static bool HasRequiredRole(ProjectRole actualRole, ProjectRole minimumRole)
    {
        return actualRole <= minimumRole;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (description is null)
        {
            return null;
        }

        var trimmed = description.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static List<Card> BuildOrderedCards(IReadOnlyList<Card> targetCards, Card movable, int insertionIndex)
    {
        var ordered = targetCards.ToList();
        var clampedIndex = Math.Clamp(insertionIndex, 0, ordered.Count);
        ordered.Insert(clampedIndex, movable);
        return ordered;
    }

    private async Task NotifySubscribersAsync(
        EntityType subscriptionEntityType,
        Guid subscriptionEntityId,
        Guid actorUserId,
        NotificationType notificationType,
        string title,
        string message,
        string? entityType,
        Guid? entityId,
        Guid? createdBy)
    {
        if (_subscriptionService is null)
        {
            return;
        }

        var subscriberIds = await _subscriptionService.GetSubscriberIdsAsync(subscriptionEntityType, subscriptionEntityId);
        if (subscriberIds.Count == 0)
        {
            return;
        }

        foreach (var subscriberId in subscriberIds.Where(x => x != actorUserId).Distinct())
        {
            await _notificationService.CreateAsync(
                subscriberId,
                notificationType,
                title,
                message,
                entityType: entityType,
                entityId: entityId,
                createdBy: createdBy);
        }
    }

    private async Task<string> GetActorLabelAsync(Guid actorUserId)
    {
        var actorDisplay = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == actorUserId)
            .Select(x => x.Email ?? x.UserName)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(actorDisplay) ? "A user" : actorDisplay;
    }
}
