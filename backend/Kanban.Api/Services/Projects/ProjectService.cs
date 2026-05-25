using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Archive;
using Kanban.Api.Services.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kanban.Api.Services.Projects;

public sealed class ProjectService : IProjectService
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 25;

    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IProjectSwimlaneService _swimlaneService;
    private readonly IActivityRecorder _activityRecorder;
    private readonly IArchivePurgeService _archivePurgeService;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        ApplicationDbContext dbContext,
        IProjectAccessGuard accessGuard,
        IProjectSwimlaneService swimlaneService,
        IActivityRecorder activityRecorder,
        IArchivePurgeService archivePurgeService,
        ILogger<ProjectService> logger)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
        _swimlaneService = swimlaneService;
        _activityRecorder = activityRecorder;
        _archivePurgeService = archivePurgeService;
        _logger = logger;
    }

    public async Task<Project> CreateAsync(Guid userId, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Project name is required.");
        }

        var trimmedName = name.Trim();
        var duplicateExists = await _dbContext.Projects
            .Where(x => x.Members.Any(m => m.UserId == userId))
            .AnyAsync(x => x.Name == trimmedName, cancellationToken);

        if (duplicateExists)
        {
            throw new ConflictException($"A project named '{trimmedName}' already exists.", "DUPLICATE_NAME");
        }

        var now = DateTime.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
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
        _activityRecorder.RecordProject(project.Id, userId, ActivityAction.Created);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task<Project> GetByIdAsync(Guid projectId, Guid userId)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId);

        if (project is null)
        {
            throw new NotFoundException("Project not found.");
        }
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer);

        return project;
    }

    public async Task<PaginatedResponse<ProjectListItemDto>> ListAsync(Guid userId, int page, int pageSize)
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
            .Select(project => new ProjectListItemDto(
                project.Id,
                project.Name,
                project.OwnerId,
                project.CreatedAt,
                project.UpdatedAt,
                project.DeletedAt,
                project.Members.Count(),
                project.Boards.Count()))
            .ToListAsync();

        return new PaginatedResponse<ProjectListItemDto>(items, effectivePage, effectivePageSize, totalCount);
    }

    public async Task<PaginatedResponse<ProjectListItemDto>> ListArchivedAsync(Guid userId, int page, int pageSize)
    {
        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize <= 0
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);

        var query = _dbContext.Projects
            .IgnoreQueryFilters()
            .Where(project => project.DeletedAt != null)
            .Where(project => project.Members.Any(member => member.UserId == userId))
            .OrderByDescending(project => project.UpdatedAt)
            .ThenBy(project => project.Id);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(project => new ProjectListItemDto(
                project.Id,
                project.Name,
                project.OwnerId,
                project.CreatedAt,
                project.UpdatedAt,
                project.DeletedAt,
                project.Members.Count(),
                project.Boards.Count()))
            .ToListAsync();

        return new PaginatedResponse<ProjectListItemDto>(items, effectivePage, effectivePageSize, totalCount);
    }

    public async Task<Project> UpdateAsync(Guid projectId, Guid userId, UpdateProjectDto data, CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);

        if (project is null)
        {
            throw new NotFoundException("Project not found.");
        }
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Owner);

        var trimmedName = data.Name.Trim();

        if (project.Name != trimmedName)
        {
            var duplicateExists = await _dbContext.Projects
                .Where(x => x.Id != projectId)
                .Where(x => x.Members.Any(m => m.UserId == userId))
                .AnyAsync(x => x.Name == trimmedName, cancellationToken);

            if (duplicateExists)
            {
                throw new ConflictException($"A project named '{trimmedName}' already exists.", "DUPLICATE_NAME");
            }
        }

        var oldName = project.Name;
        project.Name = trimmedName;
        project.UpdatedAt = DateTime.UtcNow;

        if (oldName != project.Name)
        {
            _activityRecorder.RecordProject(project.Id, userId, ActivityAction.Changed, "name", oldName, project.Name);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task<IReadOnlyList<ProjectMemberDto>> GetMembersAsync(Guid projectId, Guid userId)
    {
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer);

        return await _dbContext.ProjectMembers
            .Where(x => x.ProjectId == projectId)
            .Include(x => x.User)
            .OrderBy(x => x.Role)
            .ThenBy(x => x.JoinedAt)
            .Select(x => new ProjectMemberDto(
                x.UserId,
                x.User != null ? (x.User.Email ?? string.Empty) : string.Empty,
                x.User != null ? x.User.UserName : null,
                x.Role,
                x.JoinedAt))
            .ToListAsync();
    }

    public async Task<ProjectMember> UpdateMemberRoleAsync(Guid projectId, Guid actorUserId, Guid targetUserId, ProjectRole newRole, CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects.FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);

        if (actorUserId == targetUserId)
        {
            throw new BadRequestException("You cannot change your own role.");
        }

        if (newRole == ProjectRole.Owner)
        {
            throw new BadRequestException("Cannot set member role to owner. Use ownership transfer.");
        }

        var actorMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == actorUserId, cancellationToken);

        if (actorMembership is null || actorMembership.Role > ProjectRole.Manager)
        {
            throw new ForbiddenException("Forbidden.");
        }

        if (newRole == ProjectRole.Manager && actorMembership.Role != ProjectRole.Owner)
        {
            throw new ForbiddenException("Only the workspace owner can promote a member to manager.");
        }

        var targetMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == targetUserId, cancellationToken);

        if (targetMembership is null)
        {
            throw new NotFoundException("Project member not found.");
        }

        if (targetMembership.Role == ProjectRole.Owner)
        {
            throw new BadRequestException("Owner role cannot be changed. Use ownership transfer.");
        }

        targetMembership.Role = newRole;

        if (project is not null)
        {
            project.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return targetMembership;
    }

    public async Task RemoveMemberAsync(Guid projectId, Guid actorUserId, Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects.FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);

        if (actorUserId == targetUserId)
        {
            throw new BadRequestException("You cannot remove yourself from the project.");
        }

        var actorMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == actorUserId, cancellationToken);

        if (actorMembership is null || actorMembership.Role > ProjectRole.Manager)
        {
            throw new ForbiddenException("Forbidden.");
        }

        var targetMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == targetUserId, cancellationToken);

        if (targetMembership is null)
        {
            throw new NotFoundException("Project member not found.");
        }

        if (targetMembership.Role == ProjectRole.Owner)
        {
            throw new BadRequestException("Owner cannot be removed. Transfer ownership first.");
        }

        _dbContext.ProjectMembers.Remove(targetMembership);

        if (project is not null)
        {
            project.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Project member removed: project {ProjectId}, removed user {TargetUserId} by actor {ActorUserId}.",
            projectId, targetUserId, actorUserId);
    }

    public async Task TransferOwnershipAsync(Guid projectId, Guid currentOwnerUserId, Guid newOwnerUserId, CancellationToken cancellationToken = default)
    {
        if (currentOwnerUserId == newOwnerUserId)
        {
            throw new BadRequestException("New owner must be different from current owner.");
        }

        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);

        if (project is null)
        {
            throw new NotFoundException("Project not found.");
        }

        var currentOwnerMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == currentOwnerUserId, cancellationToken);

        if (currentOwnerMembership is null || currentOwnerMembership.Role != ProjectRole.Owner || project.OwnerId != currentOwnerUserId)
        {
            throw new ForbiddenException("Forbidden.");
        }

        var newOwnerMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == newOwnerUserId, cancellationToken);

        if (newOwnerMembership is null)
        {
            throw new BadRequestException("New owner must already be a project member.");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var now = DateTime.UtcNow;
        newOwnerMembership.Role = ProjectRole.Owner;
        currentOwnerMembership.Role = ProjectRole.Member;
        project.OwnerId = newOwnerUserId;
        project.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        _logger.LogWarning(
            "Project ownership transferred: project {ProjectId} from {OldOwnerId} to {NewOwnerId}.",
            projectId, currentOwnerUserId, newOwnerUserId);
    }

    public Task<SwimlaneView> GetSwimlaneViewAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default) =>
        _swimlaneService.GetSwimlaneViewAsync(projectId, userId, cancellationToken);

    public async Task ArchiveAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);

        if (project is null)
        {
            throw new NotFoundException("Project not found.");
        }
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Owner);

        var now = DateTime.UtcNow;
        project.DeletedAt = now;
        project.UpdatedAt = now;

        var favorites = await _dbContext.Favorites
            .Where(f => f.EntityType == EntityType.Project && f.EntityId == projectId)
            .ToListAsync(cancellationToken);
        _dbContext.Favorites.RemoveRange(favorites);

        var boardIds = await _dbContext.Boards
            .Where(b => b.ProjectId == projectId)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        var columnIds = await _dbContext.Columns
            .Where(c => boardIds.Contains(c.BoardId))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var cardIds = await _dbContext.Cards
            .Where(c => columnIds.Contains(c.ColumnId))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        var subscriptions = await _dbContext.Subscriptions
            .Where(s =>
                (s.EntityType == EntityType.Project && s.EntityId == projectId) ||
                (s.EntityType == EntityType.Board && boardIds.Contains(s.EntityId)) ||
                (s.EntityType == EntityType.Column && columnIds.Contains(s.EntityId)) ||
                (s.EntityType == EntityType.Card && cardIds.Contains(s.EntityId)))
            .ToListAsync(cancellationToken);
        _dbContext.Subscriptions.RemoveRange(subscriptions);

        _activityRecorder.RecordProject(project.Id, userId, ActivityAction.Archived);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Member, cancellationToken);

        var project = await _dbContext.Projects
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);

        if (project is null)
        {
            throw new NotFoundException("Project not found.");
        }

        project.DeletedAt = null;
        project.UpdatedAt = DateTime.UtcNow;
        _activityRecorder.RecordProject(project.Id, userId, ActivityAction.Restored);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task PurgeAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);

        if (project is null)
        {
            throw new NotFoundException("Project not found.");
        }

        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Owner, cancellationToken);

        if (project.DeletedAt is null)
        {
            throw new BadRequestException("Project must be archived before it can be permanently deleted.");
        }

        await _archivePurgeService.PurgeProjectAsync(projectId, cancellationToken);
    }

    public async Task LeaveAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        var membership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId, cancellationToken);

        if (membership is null)
        {
            throw new NotFoundException("You are not a member of this project.");
        }

        if (membership.Role == ProjectRole.Owner)
        {
            throw new ForbiddenException("Owner cannot leave the workspace. Transfer ownership first.");
        }

        _dbContext.ProjectMembers.Remove(membership);

        var project = await _dbContext.Projects.FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);
        if (project is not null)
        {
            project.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> CheckAccessAsync(Guid projectId, Guid userId, ProjectRole minimumRole)
    {
        return _accessGuard.HasAccessAsync(projectId, userId, minimumRole);
    }

}
