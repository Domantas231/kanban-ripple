namespace Kanban.Api.Services.Projects;

public interface IProjectSwimlaneService
{
    Task<SwimlaneView> GetSwimlaneViewAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
}
