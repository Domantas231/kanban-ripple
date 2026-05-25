using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Authorization;

public sealed class ProjectAccessGuard : IProjectAccessGuard
{
    private readonly ApplicationDbContext _dbContext;

    public ProjectAccessGuard(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> HasAccessAsync(Guid projectId, Guid userId, ProjectRole minimumRole, CancellationToken cancellationToken = default)
    {
        var role = await _dbContext.ProjectMembers
            .Where(x => x.ProjectId == projectId && x.UserId == userId)
            .Select(x => (ProjectRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);

        return role is not null && role.Value <= minimumRole;
    }

    public async Task RequireAccessAsync(Guid projectId, Guid userId, ProjectRole minimumRole, CancellationToken cancellationToken = default)
    {
        if (!await HasAccessAsync(projectId, userId, minimumRole, cancellationToken))
        {
            throw new ForbiddenException("Forbidden.");
        }
    }
}
