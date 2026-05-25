using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Services.Projects;
using Kanban.Api.Services.Search;
using Kanban.Api.Tests.Projects;

namespace Kanban.Api.Tests.Search;

public sealed class SearchEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public SearchEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Search_WithoutQuery_ReturnsEmptyResult()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("search-empty"));
        using var client = CreateClient(userId);

        var response = await client.GetAsync("/api/search");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GlobalSearchResult>();
        Assert.NotNull(result);
        Assert.Empty(result!.Items);
    }

    [Fact]
    public async Task Search_WithWhitespaceQuery_ReturnsEmptyResult()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("search-ws"));
        using var client = CreateClient(userId);

        var response = await client.GetAsync("/api/search?q=%20%20%20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GlobalSearchResult>();
        Assert.NotNull(result);
        Assert.Empty(result!.Items);
    }

    [Fact]
    public async Task Search_WithoutAnyProjects_ReturnsEmptyResult()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("search-no-projects"));
        using var client = CreateClient(userId);

        var response = await client.GetAsync("/api/search?q=anything");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GlobalSearchResult>();
        Assert.NotNull(result);
        Assert.Empty(result!.Items);
    }

    [Fact]
    public async Task Search_FindsProjectsAndBoardsByName()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("search-project-board"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Alpha Search Project");
        await CreateBoardAsync(client, project.Id, "Alpha Beta Board");
        await CreateBoardAsync(client, project.Id, "Unrelated Board");

        var response = await client.GetAsync("/api/search?q=Alpha");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GlobalSearchResult>();
        Assert.NotNull(result);

        Assert.Contains(result!.Items, x => x.Type == "project" && x.Name == "Alpha Search Project");
        Assert.Contains(result.Items, x => x.Type == "board" && x.Name == "Alpha Beta Board");
        Assert.DoesNotContain(result.Items, x => x.Name == "Unrelated Board");
    }

    [Fact]
    public async Task Search_FindsColumnsAndCardsByTitleOrDescription()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("search-column-card"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Engineering");
        var board = await CreateBoardAsync(client, project.Id, "Sprint Board");
        var column = await CreateColumnAsync(client, board.Id, "InProgress Tasks");
        await CreateCardAsync(client, column.Id, "Refactor InProgress Module", "InProgress refactor description");
        await CreateCardAsync(client, column.Id, "Other Card", "no match here");

        var response = await client.GetAsync("/api/search?q=InProgress");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GlobalSearchResult>();
        Assert.NotNull(result);

        Assert.Contains(result!.Items, x => x.Type == "column" && x.Name == "InProgress Tasks");
        Assert.Contains(result.Items, x => x.Type == "card" && x.Name == "Refactor InProgress Module");
    }

    [Fact]
    public async Task Search_DoesNotReturnResultsFromInaccessibleProjects()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("search-owner"));
        var outsiderUserId = await _factory.CreateUserAsync(UniqueEmail("search-outsider"));

        using var ownerClient = CreateClient(ownerUserId);
        using var outsiderClient = CreateClient(outsiderUserId);

        await CreateProjectAsync(ownerClient, "Private Project Phoenix");
        await CreateProjectAsync(outsiderClient, "Outsider Project");

        var response = await outsiderClient.GetAsync("/api/search?q=Phoenix");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<GlobalSearchResult>();
        Assert.NotNull(result);
        Assert.DoesNotContain(result!.Items, x => x.Name == "Private Project Phoenix");
    }

    [Fact]
    public async Task Search_TruncatesPerTypeToMaxResults()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("search-truncate"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Truncate Project");
        var board = await CreateBoardAsync(client, project.Id, "Truncate Board");
        var column = await CreateColumnAsync(client, board.Id, "Tasks");

        for (var i = 0; i < 8; i++)
        {
            await CreateCardAsync(client, column.Id, $"NeedleCard {i}", "needle description");
        }

        var response = await client.GetAsync("/api/search?q=NeedleCard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<GlobalSearchResult>();
        Assert.NotNull(result);

        var cardItems = result!.Items.Where(x => x.Type == "card").ToList();
        Assert.Equal(5, cardItems.Count);
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

    private static async Task<Card> CreateCardAsync(HttpClient client, Guid columnId, string title, string description)
    {
        var response = await client.PostAsJsonAsync($"/api/columns/{columnId}/cards", new
        {
            title,
            description,
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null
        });
        response.EnsureSuccessStatusCode();
        var card = await response.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(card);
        return card!;
    }
}
