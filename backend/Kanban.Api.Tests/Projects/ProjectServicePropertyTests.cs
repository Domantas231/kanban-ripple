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
