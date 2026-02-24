using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Tests.Projects;

namespace Kanban.Api.Tests.Cards;

public sealed class SubtaskEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public SubtaskEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateSubtask_ReturnsCreated_AndAssociatesWithCard()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("subtask-create-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Subtask Create Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Parent Card");

        var response = await client.PostAsJsonAsync($"/api/cards/{card.Id}/subtasks", new
        {
            description = "Write integration tests"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<Subtask>();
        Assert.NotNull(created);
        Assert.Equal(card.Id, created!.CardId);
        Assert.Equal("Write integration tests", created.Description);
        Assert.False(created.Completed);
    }

    [Fact]
    public async Task ToggleCompleted_ReturnsOk()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("subtask-toggle-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Subtask Toggle Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Parent Card");
        var subtask = await CreateSubtaskAsync(client, card.Id, "Toggle me");

        var response = await client.PutAsJsonAsync($"/api/subtasks/{subtask.Id}", new
        {
            completed = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<Subtask>();
        Assert.NotNull(updated);
        Assert.True(updated!.Completed);
    }

    [Fact]
    public async Task DeleteSubtask_ReturnsNoContent_AndSubsequentMutationReturnsNotFound()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("subtask-delete-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Subtask Delete Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Parent Card");
        var subtask = await CreateSubtaskAsync(client, card.Id, "Delete me");

        var deleteResponse = await client.DeleteAsync($"/api/subtasks/{subtask.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var updateAfterDelete = await client.PutAsJsonAsync($"/api/subtasks/{subtask.Id}", new
        {
            completed = true
        });
        Assert.Equal(HttpStatusCode.NotFound, updateAfterDelete.StatusCode);
    }

    [Fact]
    public async Task SubtaskCounts_AreAccurate_AfterToggleAndDelete()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("subtask-count-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Subtask Count Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Parent Card");

        var first = await CreateSubtaskAsync(client, card.Id, "First");
        var second = await CreateSubtaskAsync(client, card.Id, "Second");
        await CreateSubtaskAsync(client, card.Id, "Third");

        var completeFirst = await client.PutAsJsonAsync($"/api/subtasks/{first.Id}", new { completed = true });
        completeFirst.EnsureSuccessStatusCode();

        var completeSecond = await client.PutAsJsonAsync($"/api/subtasks/{second.Id}", new { completed = true });
        completeSecond.EnsureSuccessStatusCode();

        var getBeforeDelete = await client.GetAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.OK, getBeforeDelete.StatusCode);

        var cardBeforeDelete = await getBeforeDelete.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(cardBeforeDelete);
        Assert.Equal(3, cardBeforeDelete!.Subtasks.Count);
        Assert.Equal(2, cardBeforeDelete.Subtasks.Count(x => x.Completed));

        var deleteCompleted = await client.DeleteAsync($"/api/subtasks/{second.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteCompleted.StatusCode);

        var getAfterDelete = await client.GetAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.OK, getAfterDelete.StatusCode);

        var cardAfterDelete = await getAfterDelete.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(cardAfterDelete);
        Assert.Equal(2, cardAfterDelete!.Subtasks.Count);
        Assert.Equal(1, cardAfterDelete.Subtasks.Count(x => x.Completed));

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
            plannedDurationHours = 1.0m
        });
        response.EnsureSuccessStatusCode();

        var card = await response.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(card);
        return card!;
    }

    private static async Task<Subtask> CreateSubtaskAsync(HttpClient client, Guid cardId, string description)
    {
        var response = await client.PostAsJsonAsync($"/api/cards/{cardId}/subtasks", new
        {
            description
        });
        response.EnsureSuccessStatusCode();

        var subtask = await response.Content.ReadFromJsonAsync<Subtask>();
        Assert.NotNull(subtask);
        return subtask!;
    }
}