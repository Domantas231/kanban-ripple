using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Cards;

public sealed class SubtaskService : ISubtaskService
{
    private const int PositionGap = 1000;

    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IActivityRecorder _activityRecorder;

    public SubtaskService(
        ApplicationDbContext dbContext,
        IProjectAccessGuard accessGuard,
        IActivityRecorder activityRecorder)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
        _activityRecorder = activityRecorder;
    }

    public async Task<Subtask> CreateAsync(Guid cardId, Guid userId, CreateSubtaskDto data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(data.Description))
        {
            throw new BadRequestException("Subtask description is required.");
        }

        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }
        await _accessGuard.RequireAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Member);

        var maxPosition = await _dbContext.Subtasks
            .Where(x => x.CardId == cardId)
            .Select(x => (int?)x.Position)
            .MaxAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var subtask = new Subtask
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            Description = data.Description.Trim(),
            Completed = data.Completed ?? false,
            Position = (maxPosition ?? 0) + PositionGap,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Subtasks.Add(subtask);
        _activityRecorder.RecordCard(cardId, userId, ActivityAction.Added, "subtask", null, subtask.Description);
        card.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return subtask;
    }

    public async Task<Subtask> UpdateAsync(Guid subtaskId, Guid userId, UpdateSubtaskDto data, CancellationToken cancellationToken = default)
    {
        if (data.Description is null && data.Completed is null && data.Position is null)
        {
            throw new BadRequestException("At least one subtask field must be provided.");
        }

        if (data.Description is not null && string.IsNullOrWhiteSpace(data.Description))
        {
            throw new BadRequestException("Subtask description is required.");
        }

        var subtask = await _dbContext.Subtasks
            .Include(x => x.Card)
                .ThenInclude(x => x.Column)
                    .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == subtaskId, cancellationToken);

        if (subtask is null)
        {
            throw new NotFoundException("Subtask not found.");
        }
        await _accessGuard.RequireAccessAsync(subtask.Card.Column.Board.ProjectId, userId, ProjectRole.Member);

        if (data.Description is not null)
        {
            subtask.Description = data.Description.Trim();
        }

        if (data.Completed.HasValue && data.Completed.Value != subtask.Completed)
        {
            _activityRecorder.RecordCard(subtask.CardId, userId,
                data.Completed.Value ? ActivityAction.Completed : ActivityAction.Uncompleted,
                "subtask", null, subtask.Description);
            subtask.Completed = data.Completed.Value;
        }

        if (data.Position.HasValue)
        {
            subtask.Position = data.Position.Value;
        }

        var now = DateTime.UtcNow;
        subtask.UpdatedAt = now;
        subtask.Card.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return subtask;
    }

    public async Task DeleteAsync(Guid subtaskId, Guid userId, CancellationToken cancellationToken = default)
    {
        var subtask = await _dbContext.Subtasks
            .Include(x => x.Card)
                .ThenInclude(x => x.Column)
                    .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == subtaskId, cancellationToken);

        if (subtask is null)
        {
            throw new NotFoundException("Subtask not found.");
        }
        await _accessGuard.RequireAccessAsync(subtask.Card.Column.Board.ProjectId, userId, ProjectRole.Member);

        _activityRecorder.RecordCard(subtask.CardId, userId, ActivityAction.Removed, "subtask", subtask.Description, null);
        subtask.Card.UpdatedAt = DateTime.UtcNow;
        _dbContext.Subtasks.Remove(subtask);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SubtaskCountsDto> GetCountsAsync(Guid cardId, Guid userId)
    {
        var card = await _dbContext.Cards
            .AsNoTracking()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }
        await _accessGuard.RequireAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Viewer);

        var total = await _dbContext.Subtasks
            .Where(x => x.CardId == cardId)
            .CountAsync();

        var completed = await _dbContext.Subtasks
            .Where(x => x.CardId == cardId && x.Completed)
            .CountAsync();

        return new SubtaskCountsDto(completed, total);
    }

}
