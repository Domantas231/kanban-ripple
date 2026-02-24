using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Subscriptions;

public sealed class SubscriptionServicePropertyTests
{
    [Fact]
    public async Task Property_66_SubscribeCreatesSubscriptionRecord()
    {
        await using var fixture = CreateFixture();
        var userId = Guid.NewGuid();
        AddUser(fixture.DbContext, userId, UniqueEmail("property-66"));

        var projectId = await CreateProjectWithMemberAsync(fixture.DbContext, userId, ProjectRole.Viewer);

        var created = await fixture.Service.SubscribeAsync(userId, EntityType.Project, projectId);

        var persisted = await fixture.DbContext.Subscriptions
            .AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        Assert.Equal(userId, persisted.UserId);
        Assert.Equal(EntityType.Project, persisted.EntityType);
        Assert.Equal(projectId, persisted.EntityId);
        Assert.NotEqual(Guid.Empty, persisted.Id);
    }

    [Fact]
    public async Task Property_67_UnsubscribeRemovesRecordAndRoundTripCanSubscribeAgain()
    {
        await using var fixture = CreateFixture();
        var userId = Guid.NewGuid();
        AddUser(fixture.DbContext, userId, UniqueEmail("property-67"));

        var projectId = await CreateProjectWithMemberAsync(fixture.DbContext, userId, ProjectRole.Member);

        var first = await fixture.Service.SubscribeAsync(userId, EntityType.Project, projectId);
        await fixture.Service.UnsubscribeAsync(userId, EntityType.Project, projectId);

        var existsAfterUnsubscribe = await fixture.DbContext.Subscriptions
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.EntityType == EntityType.Project && x.EntityId == projectId);

        Assert.False(existsAfterUnsubscribe);

        var second = await fixture.Service.SubscribeAsync(userId, EntityType.Project, projectId);

        var allForUserAndEntity = await fixture.DbContext.Subscriptions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.EntityType == EntityType.Project && x.EntityId == projectId)
            .ToListAsync();

        Assert.Single(allForUserAndEntity);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Property_68_IsSubscribedAsyncReturnsCorrectStatus()
    {
        await using var fixture = CreateFixture();
        var subscribedUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        AddUser(fixture.DbContext, subscribedUserId, UniqueEmail("property-68-subscribed"));
        AddUser(fixture.DbContext, otherUserId, UniqueEmail("property-68-other"));

        var projectId = await CreateProjectWithMemberAsync(fixture.DbContext, subscribedUserId, ProjectRole.Owner);

        await fixture.Service.SubscribeAsync(subscribedUserId, EntityType.Project, projectId);

        var subscribedStatus = await fixture.Service.IsSubscribedAsync(subscribedUserId, EntityType.Project, projectId);
        var otherStatus = await fixture.Service.IsSubscribedAsync(otherUserId, EntityType.Project, projectId);

        Assert.True(subscribedStatus);
        Assert.False(otherStatus);

        await fixture.Service.UnsubscribeAsync(subscribedUserId, EntityType.Project, projectId);

        var statusAfterUnsubscribe = await fixture.Service.IsSubscribedAsync(subscribedUserId, EntityType.Project, projectId);
        Assert.False(statusAfterUnsubscribe);
    }

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"subscription-property-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();

        return new TestFixture(dbContext, new SubscriptionService(dbContext));
    }

    private static async Task<Guid> CreateProjectWithMemberAsync(ApplicationDbContext dbContext, Guid userId, ProjectRole role)
    {
        var projectId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        dbContext.Projects.Add(new Project
        {
            Id = projectId,
            Name = $"Project-{projectId:N}",
            Type = ProjectType.Team,
            OwnerId = userId,
            CreatedAt = now,
            UpdatedAt = now
        });

        dbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = userId,
            Role = role,
            JoinedAt = now
        });

        await dbContext.SaveChangesAsync();
        return projectId;
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

    private sealed record TestFixture(ApplicationDbContext DbContext, SubscriptionService Service) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return DbContext.DisposeAsync();
        }
    }
}
