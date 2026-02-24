using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services;
using Kanban.Api.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Subscriptions;

public sealed class SubscriptionServiceTests
{
    [Fact]
    public async Task SubscribeAsync_WithProjectAndViewerAccess_CreatesSubscription()
    {
        using var fixture = CreateFixture();
        var userId = Guid.NewGuid();

        AddUser(fixture.DbContext, userId, UniqueEmail("sub-create"));
        var projectId = await CreateProjectWithMemberAsync(fixture.DbContext, userId, ProjectRole.Viewer);

        var created = await fixture.Service.SubscribeAsync(userId, EntityType.Project, projectId);

        Assert.Equal(userId, created.UserId);
        Assert.Equal(EntityType.Project, created.EntityType);
        Assert.Equal(projectId, created.EntityId);

        var persisted = await fixture.DbContext.Subscriptions.SingleAsync();
        Assert.Equal(created.Id, persisted.Id);
    }

    [Fact]
    public async Task SubscribeAsync_DuplicateSubscription_ThrowsConflictException()
    {
        using var fixture = CreateFixture();
        var userId = Guid.NewGuid();

        AddUser(fixture.DbContext, userId, UniqueEmail("sub-duplicate"));
        var projectId = await CreateProjectWithMemberAsync(fixture.DbContext, userId, ProjectRole.Member);

        await fixture.Service.SubscribeAsync(userId, EntityType.Project, projectId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Service.SubscribeAsync(userId, EntityType.Project, projectId));
    }

    [Fact]
    public async Task UnsubscribeAsync_RemovesExistingSubscription_AndIsIdempotent()
    {
        using var fixture = CreateFixture();
        var userId = Guid.NewGuid();

        AddUser(fixture.DbContext, userId, UniqueEmail("sub-unsubscribe"));
        var projectId = await CreateProjectWithMemberAsync(fixture.DbContext, userId, ProjectRole.Member);

        await fixture.Service.SubscribeAsync(userId, EntityType.Project, projectId);
        await fixture.Service.UnsubscribeAsync(userId, EntityType.Project, projectId);
        await fixture.Service.UnsubscribeAsync(userId, EntityType.Project, projectId);

        var exists = await fixture.DbContext.Subscriptions.AnyAsync();
        Assert.False(exists);
    }

    [Fact]
    public async Task GetSubscriberIdsAndIsSubscribed_ReturnExpectedValues()
    {
        using var fixture = CreateFixture();
        var projectId = Guid.NewGuid();

        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();

        AddUser(fixture.DbContext, firstUserId, UniqueEmail("sub-status-1"));
        AddUser(fixture.DbContext, secondUserId, UniqueEmail("sub-status-2"));

        await AddProjectWithMembersAsync(
            fixture.DbContext,
            projectId,
            ownerId: firstUserId,
            memberships: new[]
            {
                (firstUserId, ProjectRole.Owner),
                (secondUserId, ProjectRole.Member)
            });

        await fixture.Service.SubscribeAsync(firstUserId, EntityType.Project, projectId);
        await fixture.Service.SubscribeAsync(secondUserId, EntityType.Project, projectId);

        var subscriberIds = await fixture.Service.GetSubscriberIdsAsync(EntityType.Project, projectId);
        Assert.Equal(2, subscriberIds.Count);
        Assert.Contains(firstUserId, subscriberIds);
        Assert.Contains(secondUserId, subscriberIds);

        var firstSubscribed = await fixture.Service.IsSubscribedAsync(firstUserId, EntityType.Project, projectId);
        var unknownSubscribed = await fixture.Service.IsSubscribedAsync(Guid.NewGuid(), EntityType.Project, projectId);

        Assert.True(firstSubscribed);
        Assert.False(unknownSubscribed);
    }

    [Fact]
    public async Task SubscribeAsync_WithoutProjectAccess_ThrowsUnauthorized()
    {
        using var fixture = CreateFixture();

        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        AddUser(fixture.DbContext, ownerId, UniqueEmail("sub-owner"));
        AddUser(fixture.DbContext, outsiderId, UniqueEmail("sub-outsider"));

        var projectId = await CreateProjectWithMemberAsync(fixture.DbContext, ownerId, ProjectRole.Owner);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Service.SubscribeAsync(outsiderId, EntityType.Project, projectId));
    }

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"subscription-service-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();

        return new TestFixture(dbContext, new SubscriptionService(dbContext));
    }

    private static async Task<Guid> CreateProjectWithMemberAsync(ApplicationDbContext dbContext, Guid userId, ProjectRole role)
    {
        var projectId = Guid.NewGuid();
        await AddProjectWithMembersAsync(
            dbContext,
            projectId,
            ownerId: userId,
            memberships: new[] { (userId, role) });

        return projectId;
    }

    private static async Task AddProjectWithMembersAsync(
        ApplicationDbContext dbContext,
        Guid projectId,
        Guid ownerId,
        IReadOnlyList<(Guid UserId, ProjectRole Role)> memberships)
    {
        var now = DateTime.UtcNow;

        dbContext.Projects.Add(new Project
        {
            Id = projectId,
            Name = $"Project-{projectId:N}",
            Type = ProjectType.Team,
            OwnerId = ownerId,
            CreatedAt = now,
            UpdatedAt = now
        });

        foreach (var membership in memberships)
        {
            dbContext.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = membership.UserId,
                Role = membership.Role,
                JoinedAt = now
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static void AddUser(ApplicationDbContext dbContext, Guid userId, string email)
    {
        dbContext.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            NormalizedUserName = email.ToUpperInvariant(),
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        dbContext.SaveChanges();
    }

    private static string UniqueEmail(string prefix)
    {
        return $"{prefix}.{Guid.NewGuid():N}@example.com";
    }

    private sealed record TestFixture(ApplicationDbContext DbContext, SubscriptionService Service) : IDisposable
    {
        public void Dispose()
        {
            DbContext.Dispose();
        }
    }
}
