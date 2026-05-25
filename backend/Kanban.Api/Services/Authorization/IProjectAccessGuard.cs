using Kanban.Api.Models;

namespace Kanban.Api.Services.Authorization;

public interface IProjectAccessGuard
{
    Task<bool> HasAccessAsync(Guid projectId, Guid userId, ProjectRole minimumRole, CancellationToken cancellationToken = default);

    Task RequireAccessAsync(Guid projectId, Guid userId, ProjectRole minimumRole, CancellationToken cancellationToken = default);
}
