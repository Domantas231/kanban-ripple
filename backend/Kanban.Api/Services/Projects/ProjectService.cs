using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Projects;

public sealed class ProjectService : IProjectService
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 25;

    private readonly ApplicationDbContext _dbContext;

    public ProjectService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Project> CreateAsync(Guid userId, string name, ProjectType type)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Project name is required.");
        }

        var now = DateTime.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Type = type,
            OwnerId = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        var membership = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = userId,
            Role = ProjectRole.Owner,
            JoinedAt = now
        };

        _dbContext.Projects.Add(project);
        _dbContext.ProjectMembers.Add(membership);

        await _dbContext.SaveChangesAsync();
        return project;
    }

    public async Task<Project> GetByIdAsync(Guid projectId, Guid userId)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId);

        if (project is null)
        {
            throw new KeyNotFoundException("Project not found.");
        }

        var hasAccess = await CheckAccessAsync(projectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        return project;
    }

    public async Task<PaginatedResponse<Project>> ListAsync(Guid userId, int page, int pageSize)
    {
        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize <= 0
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);

        var query = _dbContext.Projects
            .Where(project => project.Members.Any(member => member.UserId == userId))
            .OrderByDescending(project => project.UpdatedAt)
            .ThenBy(project => project.Id);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync();

        return new PaginatedResponse<Project>(items, effectivePage, effectivePageSize, totalCount);
    }

    public async Task<Project> UpdateAsync(Guid projectId, Guid userId, UpdateProjectDto data)
    {
        if (string.IsNullOrWhiteSpace(data.Name))
        {
            throw new InvalidOperationException("Project name is required.");
        }

        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId);

        if (project is null)
        {
            throw new KeyNotFoundException("Project not found.");
        }

        var hasAccess = await CheckAccessAsync(projectId, userId, ProjectRole.Owner);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        project.Name = data.Name.Trim();
        project.Type = data.Type;
        project.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return project;
    }

    public async Task<Project> UpgradeTypeAsync(Guid projectId, Guid userId, ProjectType newType)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId);

        if (project is null)
        {
            throw new KeyNotFoundException("Project not found.");
        }

        var hasAccess = await CheckAccessAsync(projectId, userId, ProjectRole.Owner);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        if (newType <= project.Type)
        {
            throw new InvalidOperationException($"Invalid project type transition: {project.Type} -> {newType}.");
        }

        project.Type = newType;
        project.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return project;
    }

    public async Task<IReadOnlyList<ProjectMemberDto>> GetMembersAsync(Guid projectId, Guid userId)
    {
        var hasAccess = await CheckAccessAsync(projectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        return await _dbContext.ProjectMembers
            .Where(x => x.ProjectId == projectId)
            .Include(x => x.User)
            .OrderBy(x => x.Role)
            .ThenBy(x => x.JoinedAt)
            .Select(x => new ProjectMemberDto(
                x.UserId,
                x.User != null ? (x.User.Email ?? string.Empty) : string.Empty,
                x.Role,
                x.JoinedAt))
            .ToListAsync();
    }

    public async Task<ProjectMember> UpdateMemberRoleAsync(Guid projectId, Guid actorUserId, Guid targetUserId, ProjectRole newRole)
    {
        if (actorUserId == targetUserId)
        {
            throw new InvalidOperationException("You cannot change your own role.");
        }

        if (newRole == ProjectRole.Owner)
        {
            throw new InvalidOperationException("Cannot set member role to owner. Use ownership transfer.");
        }

        var actorMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == actorUserId);

        if (actorMembership is null || !HasRequiredRole(actorMembership.Role, ProjectRole.Moderator))
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var targetMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == targetUserId);

        if (targetMembership is null)
        {
            throw new KeyNotFoundException("Project member not found.");
        }

        if (targetMembership.Role == ProjectRole.Owner)
        {
            throw new InvalidOperationException("Owner role cannot be changed. Use ownership transfer.");
        }

        targetMembership.Role = newRole;

        var project = await _dbContext.Projects.FirstOrDefaultAsync(x => x.Id == projectId);
        if (project is not null)
        {
            project.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
        return targetMembership;
    }

    public async Task RemoveMemberAsync(Guid projectId, Guid actorUserId, Guid targetUserId)
    {
        if (actorUserId == targetUserId)
        {
            throw new InvalidOperationException("You cannot remove yourself from the project.");
        }

        var actorMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == actorUserId);

        if (actorMembership is null || !HasRequiredRole(actorMembership.Role, ProjectRole.Moderator))
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var targetMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == targetUserId);

        if (targetMembership is null)
        {
            throw new KeyNotFoundException("Project member not found.");
        }

        if (targetMembership.Role == ProjectRole.Owner)
        {
            throw new InvalidOperationException("Owner cannot be removed. Transfer ownership first.");
        }

        _dbContext.ProjectMembers.Remove(targetMembership);

        var project = await _dbContext.Projects.FirstOrDefaultAsync(x => x.Id == projectId);
        if (project is not null)
        {
            project.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task TransferOwnershipAsync(Guid projectId, Guid currentOwnerUserId, Guid newOwnerUserId)
    {
        if (currentOwnerUserId == newOwnerUserId)
        {
            throw new InvalidOperationException("New owner must be different from current owner.");
        }

        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId);

        if (project is null)
        {
            throw new KeyNotFoundException("Project not found.");
        }

        var currentOwnerMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == currentOwnerUserId);

        if (currentOwnerMembership is null || currentOwnerMembership.Role != ProjectRole.Owner || project.OwnerId != currentOwnerUserId)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var newOwnerMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == newOwnerUserId);

        if (newOwnerMembership is null)
        {
            throw new KeyNotFoundException("Project member not found.");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync()
            : null;

        var now = DateTime.UtcNow;
        newOwnerMembership.Role = ProjectRole.Owner;
        currentOwnerMembership.Role = ProjectRole.Member;
        project.OwnerId = newOwnerUserId;
        project.UpdatedAt = now;

        await _dbContext.SaveChangesAsync();

        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }
    }

    public async Task ArchiveAsync(Guid projectId, Guid userId)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId);

        if (project is null)
        {
            throw new KeyNotFoundException("Project not found.");
        }

        var hasAccess = await CheckAccessAsync(projectId, userId, ProjectRole.Owner);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var now = DateTime.UtcNow;
        project.DeletedAt = now;
        project.UpdatedAt = now;

        await _dbContext.SaveChangesAsync();
    }

    public async Task RestoreAsync(Guid projectId, Guid userId)
    {
        var membership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId);

        if (membership is null || !HasRequiredRole(membership.Role, ProjectRole.Member))
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var project = await _dbContext.Projects
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == projectId);

        if (project is null)
        {
            throw new KeyNotFoundException("Project not found.");
        }

        project.DeletedAt = null;
        project.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> CheckAccessAsync(Guid projectId, Guid userId, ProjectRole minimumRole)
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
}
