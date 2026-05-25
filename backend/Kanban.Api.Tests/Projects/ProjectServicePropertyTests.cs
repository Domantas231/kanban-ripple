using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Projects;
using Kanban.Api.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Projects;

public class ProjectServicePropertyTests
{
    [Fact]
    public async Task Property_15_CreateWithoutNameReturnsError()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.ProjectService.CreateAsync(Guid.NewGuid(), ""));

        await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.ProjectService.CreateAsync(Guid.NewGuid(), "   "));
    }

    [Fact]
    public async Task Property_17_CreatorIsStoredAsOwnerInProjectMembers()
    {
        var fixture = CreateFixture();
        var creatorId = Guid.NewGuid();

        var project = await fixture.ProjectService.CreateAsync(creatorId, "Owner mapping");

        var membership = await fixture.DbContext.ProjectMembers
            .SingleAsync(x => x.ProjectId == project.Id && x.UserId == creatorId);

        Assert.Equal(ProjectRole.Owner, membership.Role);
    }

    [Fact]
    public async Task Property_18_EachCreatedProjectHasUniqueId()
    {
        var fixture = CreateFixture();
        var creatorId = Guid.NewGuid();

        var first = await fixture.ProjectService.CreateAsync(creatorId, "First");
        var second = await fixture.ProjectService.CreateAsync(creatorId, "Second");

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
    }

    [Fact]
    public async Task UpdateAsync_ChangesName()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Original");

        var updated = await fixture.ProjectService.UpdateAsync(project.Id, ownerId, new UpdateProjectDto("Renamed"));
        var persisted = await fixture.DbContext.Projects.SingleAsync(x => x.Id == project.Id);

        Assert.Equal("Renamed", updated.Name);
        Assert.Equal("Renamed", persisted.Name);
    }

    [Fact]
    public async Task Property_21_AfterMemberRemoval_ProjectAccessIsForbidden()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var removedUserId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, removedUserId, "removed@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Access revoked");
        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = removedUserId,
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        var accessibleBeforeRemoval = await fixture.ProjectService.CheckAccessAsync(project.Id, removedUserId, ProjectRole.Viewer);
        Assert.True(accessibleBeforeRemoval);

        await fixture.ProjectService.RemoveMemberAsync(project.Id, ownerId, removedUserId);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.ProjectService.GetByIdAsync(project.Id, removedUserId));
    }

    [Fact]
    public async Task Property_22_OwnerCanQueryMembersAndGetsCompleteMemberList()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, managerId, "manager@example.com");
        AddUser(fixture.DbContext, memberId, "member@example.com");
        AddUser(fixture.DbContext, viewerId, "viewer@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Complete members");
        fixture.DbContext.ProjectMembers.AddRange(
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = managerId,
                Role = ProjectRole.Manager,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = memberId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = viewerId,
                Role = ProjectRole.Viewer,
                JoinedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        var members = await fixture.ProjectService.GetMembersAsync(project.Id, ownerId);

        Assert.Equal(4, members.Count);
        Assert.Contains(members, x => x.UserId == ownerId && x.Role == ProjectRole.Owner);
        Assert.Contains(members, x => x.UserId == managerId && x.Role == ProjectRole.Manager);
        Assert.Contains(members, x => x.UserId == memberId && x.Role == ProjectRole.Member);
        Assert.Contains(members, x => x.UserId == viewerId && x.Role == ProjectRole.Viewer);
    }

    [Fact]
    public async Task GetMembersAsync_AllowsProjectMemberAndReturnsRoles()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, viewerId, "viewer@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Members view");

        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = viewerId,
            Role = ProjectRole.Viewer,
            JoinedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        var members = await fixture.ProjectService.GetMembersAsync(project.Id, viewerId);

        Assert.Equal(2, members.Count);
        Assert.Contains(members, x => x.UserId == ownerId && x.Role == ProjectRole.Owner);
        Assert.Contains(members, x => x.UserId == viewerId && x.Role == ProjectRole.Viewer);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_RequiresOwnerOrManager()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, memberId, "member@example.com");
        AddUser(fixture.DbContext, targetId, "target@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Role update");
        fixture.DbContext.ProjectMembers.AddRange(
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = memberId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = targetId,
                Role = ProjectRole.Viewer,
                JoinedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.ProjectService.UpdateMemberRoleAsync(project.Id, memberId, targetId, ProjectRole.Member));
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_DisallowsSelfRoleChangeAndOwnerAssignment()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, targetId, "target@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Role guards");
        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = targetId,
            Role = ProjectRole.Viewer,
            JoinedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.ProjectService.UpdateMemberRoleAsync(project.Id, ownerId, ownerId, ProjectRole.Manager));

        await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.ProjectService.UpdateMemberRoleAsync(project.Id, ownerId, targetId, ProjectRole.Owner));
    }

    [Fact]
    public async Task RemoveMemberAsync_RequiresOwnerOrManagerAndDisallowsSelfOrOwnerRemoval()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, managerId, "manager@example.com");
        AddUser(fixture.DbContext, memberId, "member@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Remove guards");
        fixture.DbContext.ProjectMembers.AddRange(
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = managerId,
                Role = ProjectRole.Manager,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = memberId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.ProjectService.RemoveMemberAsync(project.Id, ownerId, ownerId));

        await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.ProjectService.RemoveMemberAsync(project.Id, managerId, ownerId));
    }

    [Fact]
    public async Task Property_23_AfterRemoval_CardStillRetainsCreatedByInformation()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var removedUserId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, removedUserId, "removed@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "History");

        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = removedUserId,
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        });

        var boardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        fixture.DbContext.Boards.Add(new Board
        {
            Id = boardId,
            ProjectId = project.Id,
            Name = "Board",
            Position = 1,
            CreatedAt = now,
            UpdatedAt = now
        });

        fixture.DbContext.Columns.Add(new Column
        {
            Id = columnId,
            BoardId = boardId,
            Name = "Column",
            Position = 1,
            CreatedAt = now,
            UpdatedAt = now
        });

        fixture.DbContext.Cards.Add(new Card
        {
            Id = cardId,
            ColumnId = columnId,
            Title = "Created by soon-removed member",
            Position = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = removedUserId
        });

        await fixture.DbContext.SaveChangesAsync();

        await fixture.ProjectService.RemoveMemberAsync(project.Id, ownerId, removedUserId);

        var membershipExists = await fixture.DbContext.ProjectMembers
            .AnyAsync(x => x.ProjectId == project.Id && x.UserId == removedUserId);
        var card = await fixture.DbContext.Cards
            .Include(x => x.Creator)
            .SingleAsync(x => x.Id == cardId);

        Assert.False(membershipExists);
        Assert.Equal(removedUserId, card.CreatedBy);
        Assert.NotNull(card.Creator);
        Assert.Equal("removed@example.com", card.Creator!.Email);
    }

    [Fact]
    public async Task TransferOwnershipAsync_TransfersOwnerRoleAndProjectOwnerId()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, newOwnerId, "new-owner@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Ownership transfer");
        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = newOwnerId,
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.ProjectService.TransferOwnershipAsync(project.Id, ownerId, newOwnerId);

        var persistedProject = await fixture.DbContext.Projects.SingleAsync(x => x.Id == project.Id);
        var oldOwnerMembership = await fixture.DbContext.ProjectMembers
            .SingleAsync(x => x.ProjectId == project.Id && x.UserId == ownerId);
        var newOwnerMembership = await fixture.DbContext.ProjectMembers
            .SingleAsync(x => x.ProjectId == project.Id && x.UserId == newOwnerId);

        Assert.Equal(newOwnerId, persistedProject.OwnerId);
        Assert.Equal(ProjectRole.Member, oldOwnerMembership.Role);
        Assert.Equal(ProjectRole.Owner, newOwnerMembership.Role);
    }

    [Fact]
    public async Task TransferOwnershipAsync_RequiresCurrentOwner()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, managerId, "manager@example.com");
        AddUser(fixture.DbContext, newOwnerId, "new-owner@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Ownership guarded");
        fixture.DbContext.ProjectMembers.AddRange(
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = managerId,
                Role = ProjectRole.Manager,
                JoinedAt = DateTime.UtcNow
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = newOwnerId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(
            () => fixture.ProjectService.TransferOwnershipAsync(project.Id, managerId, newOwnerId));
    }

    [Fact]
    public async Task TransferOwnershipAsync_RequiresNewOwnerToBeProjectMember()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, outsiderId, "outsider@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Ownership member check");

        await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.ProjectService.TransferOwnershipAsync(project.Id, ownerId, outsiderId));
    }

    private static void AddUser(ApplicationDbContext dbContext, Guid userId, string email)
    {
        dbContext.Users.Add(new ApplicationUser
        {
            Id = userId,
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"project-service-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();

        var projectService = TestServiceBuilder.BuildProjectService(dbContext);

        return new TestFixture(projectService, dbContext);
    }

    private sealed record TestFixture(ProjectService ProjectService, ApplicationDbContext DbContext);
}
