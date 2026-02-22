using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Projects;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Projects;

public class ProjectServicePropertyTests
{
    [Fact]
    public async Task Property_15_CreateWithoutNameReturnsError()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ProjectService.CreateAsync(Guid.NewGuid(), "", ProjectType.Simple));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ProjectService.CreateAsync(Guid.NewGuid(), "   ", ProjectType.Team));
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("team")]
    [InlineData("full")]
    public async Task Property_16_CreateSupportedTypesAreAcceptedAndStored(string projectType)
    {
        var fixture = CreateFixture();
        var creatorId = Guid.NewGuid();
        var name = $"Project-{projectType}-{Guid.NewGuid():N}";

        var parsed = Enum.Parse<ProjectType>(projectType, ignoreCase: true);
        var created = await fixture.ProjectService.CreateAsync(creatorId, name, parsed);

        var persisted = await fixture.DbContext.Projects.SingleAsync(x => x.Id == created.Id);

        Assert.Equal(parsed, created.Type);
        Assert.Equal(parsed, persisted.Type);
        Assert.Equal(name, persisted.Name);
        Assert.Equal(creatorId, persisted.OwnerId);
    }

    [Fact]
    public async Task Property_17_CreatorIsStoredAsOwnerInProjectMembers()
    {
        var fixture = CreateFixture();
        var creatorId = Guid.NewGuid();

        var project = await fixture.ProjectService.CreateAsync(creatorId, "Owner mapping", ProjectType.Full);

        var membership = await fixture.DbContext.ProjectMembers
            .SingleAsync(x => x.ProjectId == project.Id && x.UserId == creatorId);

        Assert.Equal(ProjectRole.Owner, membership.Role);
    }

    [Fact]
    public async Task Property_18_EachCreatedProjectHasUniqueId()
    {
        var fixture = CreateFixture();
        var creatorId = Guid.NewGuid();

        var first = await fixture.ProjectService.CreateAsync(creatorId, "First", ProjectType.Simple);
        var second = await fixture.ProjectService.CreateAsync(creatorId, "Second", ProjectType.Simple);

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
    }

    [Theory]
    [InlineData(ProjectType.Simple, ProjectType.Team)]
    [InlineData(ProjectType.Simple, ProjectType.Full)]
    [InlineData(ProjectType.Team, ProjectType.Full)]
    public async Task Property_19_UpgradeTypeAllowsOnlySupportedUpgradePaths(ProjectType currentType, ProjectType targetType)
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Upgradeable project", currentType);

        var upgraded = await fixture.ProjectService.UpgradeTypeAsync(project.Id, ownerId, targetType);
        var persisted = await fixture.DbContext.Projects.SingleAsync(x => x.Id == project.Id);

        Assert.Equal(targetType, upgraded.Type);
        Assert.Equal(targetType, persisted.Type);
    }

    [Theory]
    [InlineData(ProjectType.Team, ProjectType.Simple)]
    [InlineData(ProjectType.Full, ProjectType.Team)]
    [InlineData(ProjectType.Full, ProjectType.Simple)]
    [InlineData(ProjectType.Simple, ProjectType.Simple)]
    [InlineData(ProjectType.Team, ProjectType.Team)]
    [InlineData(ProjectType.Full, ProjectType.Full)]
    public async Task Property_20_UpgradeTypeRejectsDowngradesAndNonUpgrades(ProjectType currentType, ProjectType targetType)
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "No downgrade", currentType);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ProjectService.UpgradeTypeAsync(project.Id, ownerId, targetType));
    }

    [Fact]
    public async Task Property_21_UpgradeTypeRequiresOwnerRole()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Role guarded", ProjectType.Simple);

        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = moderatorId,
            Role = ProjectRole.Moderator,
            JoinedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.ProjectService.UpgradeTypeAsync(project.Id, moderatorId, ProjectType.Team));
    }

    [Fact]
    public async Task GetMembersAsync_AllowsProjectMemberAndReturnsRoles()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, viewerId, "viewer@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Members view", ProjectType.Team);

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
    public async Task UpdateMemberRoleAsync_RequiresOwnerOrModerator()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, memberId, "member@example.com");
        AddUser(fixture.DbContext, targetId, "target@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Role update", ProjectType.Team);
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

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
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

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Role guards", ProjectType.Team);
        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = targetId,
            Role = ProjectRole.Viewer,
            JoinedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ProjectService.UpdateMemberRoleAsync(project.Id, ownerId, ownerId, ProjectRole.Moderator));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ProjectService.UpdateMemberRoleAsync(project.Id, ownerId, targetId, ProjectRole.Owner));
    }

    [Fact]
    public async Task RemoveMemberAsync_RequiresOwnerOrModeratorAndDisallowsSelfOrOwnerRemoval()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, moderatorId, "moderator@example.com");
        AddUser(fixture.DbContext, memberId, "member@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Remove guards", ProjectType.Team);
        fixture.DbContext.ProjectMembers.AddRange(
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = moderatorId,
                Role = ProjectRole.Moderator,
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ProjectService.RemoveMemberAsync(project.Id, ownerId, ownerId));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ProjectService.RemoveMemberAsync(project.Id, moderatorId, ownerId));
    }

    [Fact]
    public async Task RemoveMemberAsync_RemovesMembershipAndPreservesHistoricalCreatorReference()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var removedUserId = Guid.NewGuid();

        AddUser(fixture.DbContext, ownerId, "owner@example.com");
        AddUser(fixture.DbContext, removedUserId, "removed@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "History", ProjectType.Team);

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
            .SingleAsync(x => x.Id == cardId);

        Assert.False(membershipExists);
        Assert.Equal(removedUserId, card.CreatedBy);
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

        var projectService = new ProjectService(dbContext);

        return new TestFixture(projectService, dbContext);
    }

    private sealed record TestFixture(ProjectService ProjectService, ApplicationDbContext DbContext);
}
