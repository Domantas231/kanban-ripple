using Kanban.Api.Models;

namespace Kanban.Api.Services.Projects;

public interface IProjectService
{
    Task<Project> CreateAsync(Guid userId, string name, ProjectType type);
    Task<Project> GetByIdAsync(Guid projectId, Guid userId);
    Task<PaginatedResponse<Project>> ListAsync(Guid userId, int page, int pageSize);
    Task<Project> UpdateAsync(Guid projectId, Guid userId, UpdateProjectDto data);
    Task<Project> UpgradeTypeAsync(Guid projectId, Guid userId, ProjectType newType);
    Task<IReadOnlyList<ProjectMemberDto>> GetMembersAsync(Guid projectId, Guid userId);
    Task<ProjectMember> UpdateMemberRoleAsync(Guid projectId, Guid actorUserId, Guid targetUserId, ProjectRole newRole);
    Task RemoveMemberAsync(Guid projectId, Guid actorUserId, Guid targetUserId);
    Task ArchiveAsync(Guid projectId, Guid userId);
    Task RestoreAsync(Guid projectId, Guid userId);
    Task<bool> CheckAccessAsync(Guid projectId, Guid userId, ProjectRole minimumRole);
}
