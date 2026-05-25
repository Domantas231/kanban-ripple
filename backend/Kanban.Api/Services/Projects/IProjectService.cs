using Kanban.Api.Models;

namespace Kanban.Api.Services.Projects;

public interface IProjectService
{
    Task<Project> CreateAsync(Guid userId, string name, CancellationToken cancellationToken = default);
    Task<Project> GetByIdAsync(Guid projectId, Guid userId);
    Task<PaginatedResponse<ProjectListItemDto>> ListAsync(Guid userId, int page, int pageSize);
    Task<PaginatedResponse<ProjectListItemDto>> ListArchivedAsync(Guid userId, int page, int pageSize);
    Task<Project> UpdateAsync(Guid projectId, Guid userId, UpdateProjectDto data, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectMemberDto>> GetMembersAsync(Guid projectId, Guid userId);
    Task<ProjectMember> UpdateMemberRoleAsync(Guid projectId, Guid actorUserId, Guid targetUserId, ProjectRole newRole, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(Guid projectId, Guid actorUserId, Guid targetUserId, CancellationToken cancellationToken = default);
    Task TransferOwnershipAsync(Guid projectId, Guid currentOwnerUserId, Guid newOwnerUserId, CancellationToken cancellationToken = default);
    Task<SwimlaneView> GetSwimlaneViewAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task PurgeAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task LeaveAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CheckAccessAsync(Guid projectId, Guid userId, ProjectRole minimumRole);
}
