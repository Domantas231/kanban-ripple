using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Services.Projects;
using Kanban.Api.Services.Subscriptions;
using Kanban.Api.Tests.Projects;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Subscriptions;

public sealed class SubscriptionEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public SubscriptionEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Subscribe_ProjectMember_ReturnsCreatedSubscription()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-create"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Subscription Endpoint Project");

        var response = await client.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Project,
            entityId = project.Id
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var subscription = await response.Content.ReadFromJsonAsync<Subscription>();
        Assert.NotNull(subscription);
        Assert.Equal(userId, subscription!.UserId);
        Assert.Equal(EntityType.Project, subscription.EntityType);
        Assert.Equal(project.Id, subscription.EntityId);
    }

    [Fact]
    public async Task Delete_OnlyOwnerCanDeleteOwnSubscription()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-delete-owner"));
        var otherUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-delete-other"));

        using var ownerClient = CreateClient(ownerUserId);
        using var otherClient = CreateClient(otherUserId);

        var project = await CreateProjectAsync(ownerClient, "Subscription Delete Project");
        await AddProjectMemberAsync(project.Id, otherUserId, ProjectRole.Member);

        var created = await ownerClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Project,
            entityId = project.Id
        });
        created.EnsureSuccessStatusCode();

        var subscription = await created.Content.ReadFromJsonAsync<Subscription>();
        Assert.NotNull(subscription);

        var forbiddenDelete = await otherClient.DeleteAsync($"/api/subscriptions/{subscription!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenDelete.StatusCode);

        var ownDelete = await ownerClient.DeleteAsync($"/api/subscriptions/{subscription.Id}");
        Assert.Equal(HttpStatusCode.NoContent, ownDelete.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var exists = await db.Subscriptions.AnyAsync(x => x.Id == subscription.Id);
            Assert.False(exists);
        });
    }

    [Fact]
    public async Task GetCardSubscriptions_ProjectMemberCanReadSubscriberIds()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-list-card-owner"));
        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-list-card-member"));

        using var ownerClient = CreateClient(ownerUserId);
        using var memberClient = CreateClient(memberUserId);

        var project = await CreateProjectAsync(ownerClient, "Subscription Card List Project");
        await AddProjectMemberAsync(project.Id, memberUserId, ProjectRole.Member);

        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");
        var card = await CreateCardAsync(ownerClient, column.Id, "Card");

        await ownerClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Card,
            entityId = card.Id
        });

        await memberClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Card,
            entityId = card.Id
        });

        var response = await memberClient.GetAsync($"/api/cards/{card.Id}/subscriptions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var subscriberIds = await response.Content.ReadFromJsonAsync<List<Guid>>();
        Assert.NotNull(subscriberIds);
        Assert.Contains(ownerUserId, subscriberIds!);
        Assert.Contains(memberUserId, subscriberIds!);
    }

    [Fact]
    public async Task GetColumnAndProjectSubscriptions_NonMemberForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-list-owner"));
        var outsiderUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-list-outsider"));

        using var ownerClient = CreateClient(ownerUserId);
        using var outsiderClient = CreateClient(outsiderUserId);

        var project = await CreateProjectAsync(ownerClient, "Subscription Access Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");

        await ownerClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Column,
            entityId = column.Id
        });

        var projectResponse = await outsiderClient.GetAsync($"/api/projects/{project.Id}/subscriptions");
        var columnResponse = await outsiderClient.GetAsync($"/api/columns/{column.Id}/subscriptions");

        Assert.Equal(HttpStatusCode.Forbidden, projectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, columnResponse.StatusCode);
    }

    [Fact]
    public async Task SubscribeAndUnsubscribe_RoundTrip_RemovesSubscriberFromEntityList()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-roundtrip"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Subscription Roundtrip Project");

        var subscribeResponse = await client.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Project,
            entityId = project.Id
        });

        Assert.Equal(HttpStatusCode.OK, subscribeResponse.StatusCode);
        var subscription = await subscribeResponse.Content.ReadFromJsonAsync<Subscription>();
        Assert.NotNull(subscription);

        var beforeDelete = await client.GetFromJsonAsync<List<Guid>>($"/api/projects/{project.Id}/subscriptions");
        Assert.NotNull(beforeDelete);
        Assert.Contains(userId, beforeDelete!);

        var deleteResponse = await client.DeleteAsync($"/api/subscriptions/{subscription!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDelete = await client.GetFromJsonAsync<List<Guid>>($"/api/projects/{project.Id}/subscriptions");
        Assert.NotNull(afterDelete);
        Assert.DoesNotContain(userId, afterDelete!);
    }

    [Fact]
    public async Task Subscribe_CardColumnProject_AllEntityTypesCanBeSubscribed()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-all-types-owner"));
        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-all-types-member"));

        using var ownerClient = CreateClient(ownerUserId);
        using var memberClient = CreateClient(memberUserId);

        var project = await CreateProjectAsync(ownerClient, "Subscription Entity Types Project");
        await AddProjectMemberAsync(project.Id, memberUserId, ProjectRole.Member);
        var board = await CreateBoardAsync(ownerClient, project.Id, "Main Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");
        var card = await CreateCardAsync(ownerClient, column.Id, "Entity Type Card");

        var projectSubscribe = await memberClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Project,
            entityId = project.Id
        });

        var columnSubscribe = await memberClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Column,
            entityId = column.Id
        });

        var cardSubscribe = await memberClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Card,
            entityId = card.Id
        });

        Assert.Equal(HttpStatusCode.OK, projectSubscribe.StatusCode);
        Assert.Equal(HttpStatusCode.OK, columnSubscribe.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cardSubscribe.StatusCode);

        var projectSubscribers = await memberClient.GetFromJsonAsync<List<Guid>>($"/api/projects/{project.Id}/subscriptions");
        var columnSubscribers = await memberClient.GetFromJsonAsync<List<Guid>>($"/api/columns/{column.Id}/subscriptions");
        var cardSubscribers = await memberClient.GetFromJsonAsync<List<Guid>>($"/api/cards/{card.Id}/subscriptions");

        Assert.NotNull(projectSubscribers);
        Assert.NotNull(columnSubscribers);
        Assert.NotNull(cardSubscribers);

        Assert.Contains(memberUserId, projectSubscribers!);
        Assert.Contains(memberUserId, columnSubscribers!);
        Assert.Contains(memberUserId, cardSubscribers!);
    }

    [Fact]
    public async Task SubscribedCardChanged_SubscriberGetsNotification_ModifierDoesNot()
    {
        var modifierUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-card-modifier"));
        var subscriberUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-card-subscriber"));

        using var modifierClient = CreateClient(modifierUserId);
        using var subscriberClient = CreateClient(subscriberUserId);

        var project = await CreateProjectAsync(modifierClient, "Subscription Card Notify Project");
        await AddProjectMemberAsync(project.Id, subscriberUserId, ProjectRole.Member);

        var board = await CreateBoardAsync(modifierClient, project.Id, "Board");
        var column = await CreateColumnAsync(modifierClient, board.Id, "Todo");
        var card = await CreateCardAsync(modifierClient, column.Id, "Card To Update");

        var subscriberSubscribe = await subscriberClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Card,
            entityId = card.Id
        });
        subscriberSubscribe.EnsureSuccessStatusCode();

        var modifierSubscribe = await modifierClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Card,
            entityId = card.Id
        });
        modifierSubscribe.EnsureSuccessStatusCode();

        var updateResponse = await modifierClient.PutAsJsonAsync($"/api/cards/{card.Id}", new
        {
            title = "Card Updated",
            description = "updated",
            startDate = card.StartDate,
            dueDate = card.DueDate,
            version = card.Version
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var subscriberNotifications = await GetNotificationsAsync(subscriberClient);
        Assert.Contains(
            subscriberNotifications.Items,
            x => x.EntityType == EntityType.Card && x.EntityId == card.Id && x.CreatedBy == modifierUserId);

        var modifierNotifications = await GetNotificationsAsync(modifierClient);
        Assert.DoesNotContain(
            modifierNotifications.Items,
            x => x.EntityType == EntityType.Card && x.EntityId == card.Id && x.CreatedBy == modifierUserId);
    }

    [Fact]
    public async Task SubscribedColumnChanged_SubscriberGetsNotification_ModifierDoesNot()
    {
        var modifierUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-column-modifier"));
        var subscriberUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-column-subscriber"));

        using var modifierClient = CreateClient(modifierUserId);
        using var subscriberClient = CreateClient(subscriberUserId);

        var project = await CreateProjectAsync(modifierClient, "Subscription Column Notify Project");
        await AddProjectMemberAsync(project.Id, subscriberUserId, ProjectRole.Member);

        var board = await CreateBoardAsync(modifierClient, project.Id, "Board");
        var column = await CreateColumnAsync(modifierClient, board.Id, "Todo");

        var subscriberSubscribe = await subscriberClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Column,
            entityId = column.Id
        });
        subscriberSubscribe.EnsureSuccessStatusCode();

        var modifierSubscribe = await modifierClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Column,
            entityId = column.Id
        });
        modifierSubscribe.EnsureSuccessStatusCode();

        var updateResponse = await modifierClient.PutAsJsonAsync($"/api/columns/{column.Id}", new
        {
            name = "Doing"
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var subscriberNotifications = await GetNotificationsAsync(subscriberClient);
        Assert.Contains(
            subscriberNotifications.Items,
            x => x.EntityType == EntityType.Column && x.EntityId == column.Id && x.CreatedBy == modifierUserId);

        var modifierNotifications = await GetNotificationsAsync(modifierClient);
        Assert.DoesNotContain(
            modifierNotifications.Items,
            x => x.EntityType == EntityType.Column && x.EntityId == column.Id && x.CreatedBy == modifierUserId);
    }

    [Fact]
    public async Task SubscribedProjectChanged_SubscriberGetsNotification_ModifierDoesNot()
    {
        var modifierUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-project-modifier"));
        var subscriberUserId = await _factory.CreateUserAsync(UniqueEmail("sub-endpoint-project-subscriber"));

        using var modifierClient = CreateClient(modifierUserId);
        using var subscriberClient = CreateClient(subscriberUserId);

        var project = await CreateProjectAsync(modifierClient, "Subscription Project Notify Project");
        await AddProjectMemberAsync(project.Id, subscriberUserId, ProjectRole.Member);

        var subscriberSubscribe = await subscriberClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Project,
            entityId = project.Id
        });
        subscriberSubscribe.EnsureSuccessStatusCode();

        var modifierSubscribe = await modifierClient.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Project,
            entityId = project.Id
        });
        modifierSubscribe.EnsureSuccessStatusCode();

        var createdBoardResponse = await modifierClient.PostAsJsonAsync($"/api/projects/{project.Id}/boards", new
        {
            name = "New Board"
        });
        Assert.Equal(HttpStatusCode.Created, createdBoardResponse.StatusCode);
        var createdBoard = await createdBoardResponse.Content.ReadFromJsonAsync<Board>();
        Assert.NotNull(createdBoard);

        var subscriberNotifications = await GetNotificationsAsync(subscriberClient);
        Assert.Contains(
            subscriberNotifications.Items,
            x => x.EntityType == EntityType.Board && x.EntityId == createdBoard!.Id && x.CreatedBy == modifierUserId);

        var modifierNotifications = await GetNotificationsAsync(modifierClient);
        Assert.DoesNotContain(
            modifierNotifications.Items,
            x => x.EntityType == EntityType.Board && x.EntityId == createdBoard!.Id && x.CreatedBy == modifierUserId);
    }

    [Fact]
    public async Task GetMySubscriptions_WithMixedEntityTypes_ReturnsHydratedDtosWithLocations()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-mine-mixed"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Mine Mixed Project");
        var board = await CreateBoardAsync(client, project.Id, "Mine Board");
        var column = await CreateColumnAsync(client, board.Id, "Mine Column");
        var card = await CreateCardAsync(client, column.Id, "Mine Card");

        foreach (var entity in new[]
        {
            (EntityType.Project, project.Id),
            (EntityType.Board, board.Id),
            (EntityType.Column, column.Id),
            (EntityType.Card, card.Id),
        })
        {
            var resp = await client.PostAsJsonAsync("/api/subscriptions", new { entityType = entity.Item1, entityId = entity.Item2 });
            resp.EnsureSuccessStatusCode();
        }

        var response = await client.GetAsync("/api/subscriptions/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var mine = await response.Content.ReadFromJsonAsync<List<MySubscriptionDto>>();
        Assert.NotNull(mine);
        Assert.Equal(4, mine!.Count);

        var cardDto = mine.Single(x => x.EntityType == EntityType.Card);
        Assert.Equal("Mine Card", cardDto.EntityName);
        Assert.Equal("Mine Mixed Project", cardDto.ProjectName);
        Assert.Equal(board.Id, cardDto.BoardId);
        Assert.Equal("Mine Board", cardDto.BoardName);
        Assert.Equal("Mine Column", cardDto.ColumnName);

        var columnDto = mine.Single(x => x.EntityType == EntityType.Column);
        Assert.Equal("Mine Column", columnDto.EntityName);
        Assert.Equal(board.Id, columnDto.BoardId);

        var boardDto = mine.Single(x => x.EntityType == EntityType.Board);
        Assert.Equal("Mine Board", boardDto.EntityName);

        var projectDto = mine.Single(x => x.EntityType == EntityType.Project);
        Assert.Equal("Mine Mixed Project", projectDto.EntityName);
    }

    [Fact]
    public async Task GetMySubscriptions_DeletedEntities_ShowAsDeletedPlaceholder()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-mine-deleted"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Stale Subs Project");
        var board = await CreateBoardAsync(client, project.Id, "Stale Board");
        var column = await CreateColumnAsync(client, board.Id, "Stale Column");
        var card = await CreateCardAsync(client, column.Id, "Stale Card");

        var subResp = await client.PostAsJsonAsync("/api/subscriptions", new { entityType = EntityType.Card, entityId = card.Id });
        subResp.EnsureSuccessStatusCode();

        await _factory.WithDbContextAsync(async db =>
        {
            var stored = await db.Cards.SingleAsync(x => x.Id == card.Id);
            db.Cards.Remove(stored);
            await db.SaveChangesAsync();
        });

        var response = await client.GetAsync("/api/subscriptions/mine");
        var mine = await response.Content.ReadFromJsonAsync<List<MySubscriptionDto>>();
        Assert.NotNull(mine);
        Assert.Single(mine!);
        Assert.Equal("(deleted)", mine![0].EntityName);
    }

    [Fact]
    public async Task UnsubscribeByEntity_WithValidEntityType_RemovesSubscription()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-unsub-entity"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Unsub Entity Project");

        var subResp = await client.PostAsJsonAsync("/api/subscriptions", new { entityType = EntityType.Project, entityId = project.Id });
        subResp.EnsureSuccessStatusCode();

        var unsub = await client.DeleteAsync($"/api/subscriptions?entityType=Project&entityId={project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, unsub.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var still = await db.Subscriptions.AnyAsync(x => x.UserId == userId && x.EntityType == EntityType.Project && x.EntityId == project.Id);
            Assert.False(still);
        });
    }

    [Fact]
    public async Task UnsubscribeByEntity_WithNumericEntityType_AlsoWorks()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-unsub-numeric"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Numeric Unsub Project");
        await client.PostAsJsonAsync("/api/subscriptions", new { entityType = EntityType.Project, entityId = project.Id });

        var unsub = await client.DeleteAsync($"/api/subscriptions?entityType=2&entityId={project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, unsub.StatusCode);
    }

    [Fact]
    public async Task UnsubscribeByEntity_NonExistentSubscription_StillReturnsNoContent()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-unsub-missing"));
        using var client = CreateClient(userId);

        var unsub = await client.DeleteAsync($"/api/subscriptions?entityType=Project&entityId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NoContent, unsub.StatusCode);
    }

    [Fact]
    public async Task UnsubscribeByEntity_WithInvalidEntityType_ReturnsBadRequest()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-unsub-invalid"));
        using var client = CreateClient(userId);

        var unsub = await client.DeleteAsync($"/api/subscriptions?entityType=Banana&entityId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.BadRequest, unsub.StatusCode);
    }

    [Fact]
    public async Task UnsubscribeByEntity_WithMissingEntityType_ReturnsBadRequest()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-unsub-blank"));
        using var client = CreateClient(userId);

        var unsub = await client.DeleteAsync($"/api/subscriptions?entityType=&entityId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.BadRequest, unsub.StatusCode);
    }

    [Fact]
    public async Task UnsubscribeById_WithUnknownId_Returns404()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-unsub-unknown"));
        using var client = CreateClient(userId);

        var response = await client.DeleteAsync($"/api/subscriptions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Subscribe_DuplicateSubscription_ReturnsConflict()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-dup"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Dup Sub Project");

        var first = await client.PostAsJsonAsync("/api/subscriptions", new { entityType = EntityType.Project, entityId = project.Id });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/subscriptions", new { entityType = EntityType.Project, entityId = project.Id });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Subscribe_WithEmptyEntityId_ReturnsBadRequest()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-empty-entity"));
        using var client = CreateClient(userId);

        var response = await client.PostAsJsonAsync("/api/subscriptions", new { entityType = EntityType.Project, entityId = Guid.Empty });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Subscribe_WithUnknownProject_ReturnsNotFound()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sub-unknown-project"));
        using var client = CreateClient(userId);

        var response = await client.PostAsJsonAsync("/api/subscriptions", new { entityType = EntityType.Project, entityId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardSubscriptions_ReturnsSubscriberIds()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("sub-board-owner"));
        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("sub-board-member"));

        using var ownerClient = CreateClient(ownerUserId);
        using var memberClient = CreateClient(memberUserId);

        var project = await CreateProjectAsync(ownerClient, "Board Subs Project");
        await AddProjectMemberAsync(project.Id, memberUserId, ProjectRole.Member);

        var board = await CreateBoardAsync(ownerClient, project.Id, "Subscribed Board");

        await ownerClient.PostAsJsonAsync("/api/subscriptions", new { entityType = EntityType.Board, entityId = board.Id });
        await memberClient.PostAsJsonAsync("/api/subscriptions", new { entityType = EntityType.Board, entityId = board.Id });

        var response = await ownerClient.GetAsync($"/api/boards/{board.Id}/subscriptions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var subscribers = await response.Content.ReadFromJsonAsync<List<Guid>>();
        Assert.NotNull(subscribers);
        Assert.Contains(ownerUserId, subscribers!);
        Assert.Contains(memberUserId, subscribers);
    }

    [Fact]
    public async Task GetCardSubscriptions_OutsiderForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("sub-card-fb-owner"));
        var outsiderUserId = await _factory.CreateUserAsync(UniqueEmail("sub-card-fb-outsider"));

        using var ownerClient = CreateClient(ownerUserId);
        using var outsiderClient = CreateClient(outsiderUserId);

        var project = await CreateProjectAsync(ownerClient, "Forbidden Card Subs Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");
        var card = await CreateCardAsync(ownerClient, column.Id, "Card");

        var response = await outsiderClient.GetAsync($"/api/cards/{card.Id}/subscriptions");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());
        return client;
    }

    private async Task AddProjectMemberAsync(Guid projectId, Guid userId, ProjectRole role)
    {
        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = userId,
                Role = role,
                JoinedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        });
    }

    private static string UniqueEmail(string prefix)
    {
        return $"{prefix}.{Guid.NewGuid():N}@example.com";
    }

    private static async Task<PaginatedResponse<Notification>> GetNotificationsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/notifications?page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PaginatedResponse<Notification>>();
        Assert.NotNull(payload);
        return payload!;
    }

    private static async Task<Project> CreateProjectAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new { name });
        response.EnsureSuccessStatusCode();

        var project = await response.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);
        return project!;
    }

    private static async Task<Board> CreateBoardAsync(HttpClient client, Guid projectId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/boards", new { name });
        response.EnsureSuccessStatusCode();

        var board = await response.Content.ReadFromJsonAsync<Board>();
        Assert.NotNull(board);
        return board!;
    }

    private static async Task<Column> CreateColumnAsync(HttpClient client, Guid boardId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/columns", new { name });
        response.EnsureSuccessStatusCode();

        var column = await response.Content.ReadFromJsonAsync<Column>();
        Assert.NotNull(column);
        return column!;
    }

    private static async Task<Card> CreateCardAsync(HttpClient client, Guid columnId, string title)
    {
        var response = await client.PostAsJsonAsync($"/api/columns/{columnId}/cards", new
        {
            title,
            description = "desc",
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null
        });
        response.EnsureSuccessStatusCode();

        var card = await response.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(card);
        return card!;
    }
}
