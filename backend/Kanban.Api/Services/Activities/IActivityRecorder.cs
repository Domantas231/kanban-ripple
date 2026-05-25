using Kanban.Api.Models;

namespace Kanban.Api.Services.Activities;

public interface IActivityRecorder
{
    void RecordCard(Guid cardId, Guid userId, ActivityAction action, string? field = null, string? oldValue = null, string? newValue = null);
    void RecordBoard(Guid boardId, Guid userId, ActivityAction action, string? field = null, string? oldValue = null, string? newValue = null);
    void RecordColumn(Guid columnId, Guid userId, ActivityAction action, string? field = null, string? oldValue = null, string? newValue = null);
    void RecordProject(Guid projectId, Guid userId, ActivityAction action, string? field = null, string? oldValue = null, string? newValue = null);
}
