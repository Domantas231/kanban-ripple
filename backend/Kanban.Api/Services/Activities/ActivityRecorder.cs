using Kanban.Api.Data;
using Kanban.Api.Models;

namespace Kanban.Api.Services.Activities;

public sealed class ActivityRecorder : IActivityRecorder
{
    private const int MaxValueLength = 500;

    private readonly ApplicationDbContext _dbContext;

    public ActivityRecorder(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void RecordCard(Guid cardId, Guid userId, ActivityAction action, string? field = null, string? oldValue = null, string? newValue = null)
    {
        _dbContext.CardActivities.Add(new CardActivity
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = userId,
            Action = action,
            Field = field,
            OldValue = Truncate(oldValue),
            NewValue = Truncate(newValue),
            CreatedAt = DateTime.UtcNow
        });
    }

    public void RecordBoard(Guid boardId, Guid userId, ActivityAction action, string? field = null, string? oldValue = null, string? newValue = null)
    {
        _dbContext.BoardActivities.Add(new BoardActivity
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            UserId = userId,
            Action = action,
            Field = field,
            OldValue = Truncate(oldValue),
            NewValue = Truncate(newValue),
            CreatedAt = DateTime.UtcNow
        });
    }

    public void RecordColumn(Guid columnId, Guid userId, ActivityAction action, string? field = null, string? oldValue = null, string? newValue = null)
    {
        _dbContext.ColumnActivities.Add(new ColumnActivity
        {
            Id = Guid.NewGuid(),
            ColumnId = columnId,
            UserId = userId,
            Action = action,
            Field = field,
            OldValue = Truncate(oldValue),
            NewValue = Truncate(newValue),
            CreatedAt = DateTime.UtcNow
        });
    }

    public void RecordProject(Guid projectId, Guid userId, ActivityAction action, string? field = null, string? oldValue = null, string? newValue = null)
    {
        _dbContext.ProjectActivities.Add(new ProjectActivity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = userId,
            Action = action,
            Field = field,
            OldValue = Truncate(oldValue),
            NewValue = Truncate(newValue),
            CreatedAt = DateTime.UtcNow
        });
    }

    private static string? Truncate(string? value) =>
        value?.Length > MaxValueLength ? value[..MaxValueLength] : value;
}
