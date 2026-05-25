using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Services.Favorites;
using Kanban.Api.Tests.Projects;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Favorites;

public sealed class FavoriteEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public FavoriteEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_WithNoFavorites_ReturnsEmpty()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("fav-list-empty"));
        using var client = CreateClient(userId);

        var response = await client.GetAsync("/api/favorites");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var favorites = await response.Content.ReadFromJsonAsync<List<FavoriteDto>>();
        Assert.NotNull(favorites);
        Assert.Empty(favorites!);
    }

    [Fact]
    public async Task Toggle_OnNewEntity_AddsFavorite()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("fav-toggle-add"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Toggle Add Project");

        var response = await client.PostAsJsonAsync("/api/favorites/toggle", new
        {
            entityType = EntityType.Project,
            entityId = project.Id
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var favorite = await response.Content.ReadFromJsonAsync<FavoriteDto>();
        Assert.NotNull(favorite);
        Assert.Equal(EntityType.Project, favorite!.EntityType);
        Assert.Equal(project.Id, favorite.EntityId);

        var listResponse = await client.GetAsync("/api/favorites");
        var list = await listResponse.Content.ReadFromJsonAsync<List<FavoriteDto>>();
        Assert.Single(list!);
    }

    [Fact]
    public async Task Toggle_OnExistingFavorite_RemovesIt()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("fav-toggle-remove"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Toggle Remove Project");

        await client.PostAsJsonAsync("/api/favorites/toggle", new
        {
            entityType = EntityType.Project,
            entityId = project.Id
        });

        var second = await client.PostAsJsonAsync("/api/favorites/toggle", new
        {
            entityType = EntityType.Project,
            entityId = project.Id
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var listResponse = await client.GetAsync("/api/favorites");
        var list = await listResponse.Content.ReadFromJsonAsync<List<FavoriteDto>>();
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Toggle_ProjectFavorite_CreatesSubscriptionAutomatically()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("fav-project-sub"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Auto Subscribe Project");

        var response = await client.PostAsJsonAsync("/api/favorites/toggle", new
        {
            entityType = EntityType.Project,
            entityId = project.Id
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var sub = await db.Subscriptions.FirstOrDefaultAsync(s =>
                s.UserId == userId && s.EntityType == EntityType.Project && s.EntityId == project.Id);
            Assert.NotNull(sub);
        });
    }

    [Fact]
    public async Task Toggle_BoardFavorite_CreatesSubscriptionAutomatically()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("fav-board-sub"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Auto Subscribe Board Project");
        var board = await CreateBoardAsync(client, project.Id, "Subscribed Board");

        var response = await client.PostAsJsonAsync("/api/favorites/toggle", new
        {
            entityType = EntityType.Board,
            entityId = board.Id
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var sub = await db.Subscriptions.FirstOrDefaultAsync(s =>
                s.UserId == userId && s.EntityType == EntityType.Board && s.EntityId == board.Id);
            Assert.NotNull(sub);
        });
    }

    [Fact]
    public async Task Toggle_CardFavorite_DoesNotCreateSubscription()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("fav-card-no-sub"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Card Fav Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");

        var response = await client.PostAsJsonAsync("/api/favorites/toggle", new
        {
            entityType = EntityType.Card,
            entityId = card.Id
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var sub = await db.Subscriptions.FirstOrDefaultAsync(s =>
                s.UserId == userId && s.EntityType == EntityType.Card && s.EntityId == card.Id);
            Assert.Null(sub);
        });
    }

    [Fact]
    public async Task Toggle_DoesNotDuplicateSubscriptionWhenAlreadySubscribed()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("fav-no-dup-sub"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "No Dup Project");

        var subscribe = await client.PostAsJsonAsync("/api/subscriptions", new
        {
            entityType = EntityType.Project,
            entityId = project.Id
        });
        subscribe.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/favorites/toggle", new
        {
            entityType = EntityType.Project,
            entityId = project.Id
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var subs = await db.Subscriptions
                .Where(s => s.UserId == userId && s.EntityType == EntityType.Project && s.EntityId == project.Id)
                .ToListAsync();
            Assert.Single(subs);
        });
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());
        return client;
    }

    private static string UniqueEmail(string prefix)
    {
        return $"{prefix}.{Guid.NewGuid():N}@example.com";
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
