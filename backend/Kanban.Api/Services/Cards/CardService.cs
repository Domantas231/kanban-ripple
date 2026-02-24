using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Cards;

public sealed class CardService : ICardService
{
    private const int PositionGap = 1000;

    private readonly ApplicationDbContext _dbContext;

    public CardService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
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

        return card;
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
}
