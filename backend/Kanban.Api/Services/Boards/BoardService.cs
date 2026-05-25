using System.Text;
using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Authorization;
using Kanban.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kanban.Api.Services.Boards;

public sealed class BoardService : IBoardService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IProjectBroadcaster _projectBroadcaster;
    private readonly INotificationFanout _notificationFanout;
    private readonly IActivityRecorder _activityRecorder;
    private readonly IBoardArchiveService _archiveService;
    private readonly ILogger<BoardService> _logger;

    public BoardService(
        ApplicationDbContext dbContext,
        IProjectAccessGuard accessGuard,
        IActivityRecorder activityRecorder,
        IBoardArchiveService archiveService,
        INotificationFanout notificationFanout,
        IProjectBroadcaster projectBroadcaster,
        ILogger<BoardService> logger)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
        _activityRecorder = activityRecorder;
        _archiveService = archiveService;
        _notificationFanout = notificationFanout;
        _projectBroadcaster = projectBroadcaster;
        _logger = logger;
    }

    public async Task<Board> CreateAsync(Guid projectId, Guid userId, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Board name is required.");
        }

        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Member);

        var projectExists = await _dbContext.Projects.AnyAsync(x => x.Id == projectId, cancellationToken);
        if (!projectExists)
        {
            throw new NotFoundException("Project not found.");
        }

        var trimmedName = name.Trim();
        var duplicateExists = await _dbContext.Boards
            .Where(x => x.ProjectId == projectId)
            .AnyAsync(x => x.Name == trimmedName, cancellationToken);

        if (duplicateExists)
        {
            throw new ConflictException($"A board named '{trimmedName}' already exists in this project.", "DUPLICATE_NAME");
        }

        var maxPosition = await _dbContext.Boards
            .Where(x => x.ProjectId == projectId)
            .Select(x => (int?)x.Position)
            .MaxAsync(cancellationToken) ?? 0;

        var now = DateTime.UtcNow;
        var board = new Board
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = trimmedName,
            Position = maxPosition + 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Boards.Add(board);
        _activityRecorder.RecordBoard(board.Id, userId, ActivityAction.Created);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _projectBroadcaster.BoardCreated(projectId, board);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Created,
            $"Board created: {board.Name}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} added board '{board.Name}' to the project.",
            entityType: EntityType.Board,
            entityId: board.Id,
            createdBy: userId,
            (EntityType.Project, projectId),
            (EntityType.Board, board.Id));

        return board;
    }

    private static readonly Dictionary<string, string> TrelloColorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["green"] = "#61bd4f",
        ["yellow"] = "#f2d600",
        ["orange"] = "#ff9f1a",
        ["red"] = "#eb5a46",
        ["purple"] = "#c377e0",
        ["blue"] = "#0079bf",
        ["sky"] = "#00c2e0",
        ["lime"] = "#51e898",
        ["pink"] = "#ff78cb",
        ["black"] = "#344563",
        ["green_dark"] = "#519839",
        ["yellow_dark"] = "#d9b51c",
        ["orange_dark"] = "#d29034",
        ["red_dark"] = "#b04632",
        ["purple_dark"] = "#89609e",
        ["blue_dark"] = "#055a8c",
        ["sky_dark"] = "#0098b7",
        ["lime_dark"] = "#4bbf6b",
        ["pink_dark"] = "#cd5a91",
        ["black_dark"] = "#091e42",
        ["green_light"] = "#b3f1b0",
        ["yellow_light"] = "#fce682",
        ["orange_light"] = "#ffc278",
        ["red_light"] = "#f5a3a0",
        ["purple_light"] = "#deb8f0",
        ["blue_light"] = "#8bbdd9",
        ["sky_light"] = "#8fdfeb",
        ["lime_light"] = "#b3f1b0",
        ["pink_light"] = "#ffbdcb",
        ["black_light"] = "#8c9bab",
    };

    private static string? BuildImportedDescription(string? originalDesc, IReadOnlyList<TrelloAttachment> attachments)
    {
        var validAttachments = attachments
            .Where(a => !string.IsNullOrWhiteSpace(a.Url))
            .ToList();

        if (validAttachments.Count == 0)
        {
            return string.IsNullOrWhiteSpace(originalDesc) ? null : originalDesc;
        }

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(originalDesc))
        {
            sb.Append(originalDesc.TrimEnd());
            sb.AppendLine();
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("> Imported from Trello — attachment URLs may expire over time.");
        sb.AppendLine();
        sb.AppendLine("**Attachments:**");

        foreach (var attachment in validAttachments)
        {
            var label = string.IsNullOrWhiteSpace(attachment.Name)
                ? attachment.Url
                : attachment.Name.Trim();
            sb.AppendLine($"- [{label}]({attachment.Url})");
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<Board> ImportFromTrelloAsync(Guid projectId, Guid userId, TrelloImportRequest trelloData, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trelloData.Name))
        {
            throw new BadRequestException("Board name is required in the Trello export.");
        }
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Member);

        var projectExists = await _dbContext.Projects.AnyAsync(x => x.Id == projectId, cancellationToken);
        if (!projectExists)
        {
            throw new NotFoundException("Project not found.");
        }

        var trimmedBoardName = trelloData.Name.Trim();
        var duplicateBoardExists = await _dbContext.Boards
            .Where(x => x.ProjectId == projectId)
            .AnyAsync(x => x.Name == trimmedBoardName, cancellationToken);

        if (duplicateBoardExists)
        {
            throw new ConflictException($"A board named '{trimmedBoardName}' already exists in this project.", "DUPLICATE_NAME");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var now = DateTime.UtcNow;

        var maxBoardPosition = await _dbContext.Boards
            .Where(x => x.ProjectId == projectId)
            .Select(x => (int?)x.Position)
            .MaxAsync(cancellationToken) ?? 0;

        var board = new Board
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = trimmedBoardName,
            Position = maxBoardPosition + 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.Boards.Add(board);

        // Create columns from all Trello lists (archived ones imported and marked archived).
        var sortedLists = trelloData.Lists.OrderBy(l => l.Pos).ToList();

        var trelloListIdToColumnId = new Dictionary<string, Guid>();
        var archivedColumnIds = new HashSet<Guid>();
        for (var i = 0; i < sortedLists.Count; i++)
        {
            var trelloList = sortedLists[i];
            var column = new Column
            {
                Id = Guid.NewGuid(),
                BoardId = board.Id,
                Name = trelloList.Name.Trim(),
                Position = (i + 1) * 1000,
                CreatedAt = now,
                UpdatedAt = now,
                DeletedAt = trelloList.Closed ? now : null
            };
            _dbContext.Columns.Add(column);
            trelloListIdToColumnId[trelloList.Id] = column.Id;
            if (trelloList.Closed)
            {
                archivedColumnIds.Add(column.Id);
            }
        }

        var existingTags = await _dbContext.Tags
            .Where(x => x.BoardId == board.Id)
            .ToListAsync(cancellationToken);

        var trelloLabelIdToTagId = new Dictionary<string, Guid>();
        foreach (var label in trelloData.Labels.Where(l => !string.IsNullOrWhiteSpace(l.Name)))
        {
            var normalizedName = label.Name.Trim();
            var existingTag = existingTags.FirstOrDefault(t =>
                string.Equals(t.Name, normalizedName, StringComparison.OrdinalIgnoreCase));

            if (existingTag is not null)
            {
                trelloLabelIdToTagId[label.Id] = existingTag.Id;
                continue;
            }

            var hexColor = "#808080"; // default gray
            if (!string.IsNullOrEmpty(label.Color) && TrelloColorMap.TryGetValue(label.Color, out var mapped))
            {
                hexColor = mapped;
            }

            var tag = new Tag
            {
                Id = Guid.NewGuid(),
                BoardId = board.Id,
                Name = normalizedName,
                Color = hexColor,
                CreatedAt = now
            };
            _dbContext.Tags.Add(tag);
            existingTags.Add(tag);
            trelloLabelIdToTagId[label.Id] = tag.Id;
        }

        // Create cards from Trello cards (archived ones are imported and marked archived).
        // Cards in archived lists are still skipped because their column doesn't exist.
        var cardsByList = trelloData.Cards
            .Where(c => trelloListIdToColumnId.ContainsKey(c.IdList))
            .GroupBy(c => c.IdList)
            .ToList();

        var trelloCardIdToCardId = new Dictionary<string, Guid>();
        var archivedCardIds = new HashSet<Guid>();

        foreach (var group in cardsByList)
        {
            var columnId = trelloListIdToColumnId[group.Key];
            var sortedCards = group.OrderBy(c => c.Pos).ToList();

            for (var i = 0; i < sortedCards.Count; i++)
            {
                var trelloCard = sortedCards[i];
                var isArchived = trelloCard.Closed || archivedColumnIds.Contains(columnId);
                var card = new Card
                {
                    Id = Guid.NewGuid(),
                    ColumnId = columnId,
                    Title = trelloCard.Name.Trim(),
                    Description = BuildImportedDescription(trelloCard.Desc, trelloCard.Attachments),
                    StartDate = trelloCard.Start?.ToUniversalTime(),
                    DueDate = trelloCard.Due?.ToUniversalTime(),
                    Position = (i + 1) * 1000,
                    Version = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                    DeletedAt = isArchived ? now : null,
                    CreatedBy = userId
                };
                _dbContext.Cards.Add(card);
                trelloCardIdToCardId[trelloCard.Id] = card.Id;
                if (isArchived)
                {
                    archivedCardIds.Add(card.Id);
                }

                foreach (var labelId in trelloCard.IdLabels)
                {
                    if (trelloLabelIdToTagId.TryGetValue(labelId, out var tagId))
                    {
                        _dbContext.Set<CardTag>().Add(new CardTag
                        {
                            Id = Guid.NewGuid(),
                            CardId = card.Id,
                            TagId = tagId,
                            CreatedAt = now
                        });
                    }
                }
            }
        }

        var checklistsByCard = trelloData.Checklists
            .Where(cl => trelloCardIdToCardId.ContainsKey(cl.IdCard))
            .GroupBy(cl => cl.IdCard);

        foreach (var checklistGroup in checklistsByCard)
        {
            var cardId = trelloCardIdToCardId[checklistGroup.Key];
            var orderedChecklists = checklistGroup.OrderBy(cl => cl.Pos).ToList();
            var prefixWithChecklistName = orderedChecklists.Count > 1;
            var position = 0;

            foreach (var checklist in orderedChecklists)
            {
                foreach (var item in checklist.CheckItems.OrderBy(ci => ci.Pos))
                {
                    if (string.IsNullOrWhiteSpace(item.Name))
                    {
                        continue;
                    }

                    position++;
                    var description = prefixWithChecklistName && !string.IsNullOrWhiteSpace(checklist.Name)
                        ? $"{checklist.Name.Trim()}: {item.Name.Trim()}"
                        : item.Name.Trim();

                    _dbContext.Subtasks.Add(new Subtask
                    {
                        Id = Guid.NewGuid(),
                        CardId = cardId,
                        Description = description,
                        Completed = string.Equals(item.State, "complete", StringComparison.OrdinalIgnoreCase),
                        Position = position * 1000,
                        CreatedAt = now,
                        UpdatedAt = now,
                        DeletedAt = archivedCardIds.Contains(cardId) ? now : null
                    });
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Imported Trello board {BoardName} ({BoardId}) into project {ProjectId} for user {UserId}.",
            board.Name, board.Id, projectId, userId);

        await _projectBroadcaster.BoardCreated(projectId, board);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Created,
            $"Board imported: {board.Name}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} imported board '{board.Name}' from Trello.",
            entityType: EntityType.Board,
            entityId: board.Id,
            createdBy: userId,
            (EntityType.Project, projectId),
            (EntityType.Board, board.Id));

        return board;
    }

    public async Task<Board> GetByIdAsync(Guid boardId, Guid userId)
    {
        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == boardId);

        if (board is null)
        {
            throw new NotFoundException("Board not found.");
        }
        await _accessGuard.RequireAccessAsync(board.ProjectId, userId, ProjectRole.Viewer);

        return board;
    }

    public async Task<IReadOnlyList<Board>> ListAsync(Guid projectId, Guid userId)
    {
        var projectExists = await _dbContext.Projects.AnyAsync(x => x.Id == projectId);
        if (!projectExists)
        {
            throw new NotFoundException("Project not found.");
        }
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer);

        var boards = await _dbContext.Boards
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Select(b => new
            {
                Board = b,
                ColumnCount = b.Columns.Count(),
                CardCount = b.Columns.SelectMany(c => c.Cards).Count(),
                LatestActivity = new[]
                {
                    b.UpdatedAt,
                    b.Columns.Max(c => (DateTime?)c.UpdatedAt) ?? b.UpdatedAt,
                    b.Columns.SelectMany(c => c.Cards).Max(card => (DateTime?)card.UpdatedAt) ?? b.UpdatedAt
                }.Max()
            })
            .OrderByDescending(x => x.LatestActivity)
            .ThenByDescending(x => x.Board.Id)
            .ToListAsync();

        foreach (var entry in boards)
        {
            entry.Board.UpdatedAt = entry.LatestActivity;
            entry.Board.ColumnCount = entry.ColumnCount;
            entry.Board.CardCount = entry.CardCount;
        }

        return boards.Select(x => x.Board).ToList();
    }

    public async Task<IReadOnlyList<Board>> ListArchivedAsync(Guid userId)
    {
        return await _dbContext.Boards
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.DeletedAt != null)
            .Where(x => _dbContext.ProjectMembers.Any(pm =>
                pm.ProjectId == x.ProjectId
                && pm.UserId == userId
                && pm.Role <= ProjectRole.Viewer))
            .OrderByDescending(x => x.DeletedAt)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<Board> UpdateAsync(Guid boardId, Guid userId, UpdateBoardDto data, CancellationToken cancellationToken = default)
    {
        var board = await _dbContext.Boards
            .FirstOrDefaultAsync(x => x.Id == boardId, cancellationToken);

        if (board is null)
        {
            throw new NotFoundException("Board not found.");
        }
        await _accessGuard.RequireAccessAsync(board.ProjectId, userId, ProjectRole.Member);

        var trimmedName = data.Name.Trim();

        if (board.Name != trimmedName)
        {
            var duplicateExists = await _dbContext.Boards
                .Where(x => x.Id != boardId)
                .Where(x => x.ProjectId == board.ProjectId)
                .AnyAsync(x => x.Name == trimmedName, cancellationToken);

            if (duplicateExists)
            {
                throw new ConflictException($"A board named '{trimmedName}' already exists in this project.", "DUPLICATE_NAME");
            }
        }

        var oldName = board.Name;
        board.Name = trimmedName;
        board.Position = data.Position;
        board.UpdatedAt = DateTime.UtcNow;

        if (oldName != board.Name)
        {
            _activityRecorder.RecordBoard(board.Id, userId, ActivityAction.Changed, "name", oldName, board.Name);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _projectBroadcaster.BoardUpdated(board.ProjectId, board);

        await _notificationFanout.FanOutAsync(
            userId,
            NotificationType.Updated,
            $"Board updated: {board.Name}",
            $"{await _notificationFanout.GetActorLabelAsync(userId)} updated board '{board.Name}'.",
            entityType: EntityType.Board,
            entityId: board.Id,
            createdBy: userId,
            (EntityType.Board, board.Id),
            (EntityType.Project, board.ProjectId));

        return board;
    }

    public Task ArchiveAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default) =>
        _archiveService.ArchiveAsync(boardId, userId, cancellationToken);

    public Task RestoreAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default) =>
        _archiveService.RestoreAsync(boardId, userId, cancellationToken);

    public Task PurgeAsync(Guid boardId, Guid userId, CancellationToken cancellationToken = default) =>
        _archiveService.PurgeAsync(boardId, userId, cancellationToken);

}
