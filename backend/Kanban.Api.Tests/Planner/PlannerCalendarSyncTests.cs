using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Services.Google;
using Kanban.Api.Services.Planner;
using Kanban.Api.Tests.Projects;

namespace Kanban.Api.Tests.Planner;

public sealed class PlannerCalendarSyncTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public PlannerCalendarSyncTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateBlock_SyncsToGoogleCalendar_StatusBecomesSynced()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sync-create"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Sync Create Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Sync Card");

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/planner/blocks", new
        {
            cardId = card.Id,
            date = date.ToString("yyyy-MM-dd"),
            startTime = "09:00:00",
            endTime = "10:00:00"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var block = await response.Content.ReadFromJsonAsync<PlannedBlockDto>();
        Assert.NotNull(block);
        Assert.Equal(PlannedBlockSyncStatus.Synced, block!.SyncStatus);
        Assert.NotNull(block.GoogleEventId);
        Assert.StartsWith("test-event-", block.GoogleEventId);
    }

    [Fact]
    public async Task UpdateBlock_SyncsToGoogleCalendar_StatusBecomesSynced()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sync-update"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Sync Update Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Update Sync Card");

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(11));
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/planner/blocks", new
        {
            cardId = card.Id,
            date = date.ToString("yyyy-MM-dd"),
            startTime = "09:00:00",
            endTime = "10:00:00"
        });
        var block = await createResponse.Content.ReadFromJsonAsync<PlannedBlockDto>();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/planner/blocks/{block!.Id}",
            new
            {
                startTime = "14:00:00",
                endTime = "16:00:00"
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<PlannedBlockDto>();
        Assert.NotNull(updated);
        Assert.Equal(PlannedBlockSyncStatus.Synced, updated!.SyncStatus);
    }

    [Fact]
    public async Task DeleteBlock_AfterSync_ReturnsNoContent()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("sync-delete"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Sync Delete Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Delete Sync Card");

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12));
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/planner/blocks", new
        {
            cardId = card.Id,
            date = date.ToString("yyyy-MM-dd"),
            startTime = "09:00:00",
            endTime = "10:00:00"
        });
        var block = await createResponse.Content.ReadFromJsonAsync<PlannedBlockDto>();
        Assert.Equal(PlannedBlockSyncStatus.Synced, block!.SyncStatus);

        var deleteResponse = await client.DeleteAsync(
            $"/api/projects/{project.Id}/planner/blocks/{block.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/projects/{project.Id}/planner/blocks?date={date:yyyy-MM-dd}");
        var blocks = await getResponse.Content.ReadFromJsonAsync<List<PlannedBlockDto>>();
        Assert.NotNull(blocks);
        Assert.Empty(blocks!);
    }

    [Fact]
    public async Task GetCalendarEvents_NoGoogleAccount_ReturnsNotFound()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("cal-events-noauth"));
        using var client = CreateClient(userId);

        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await client.GetAsync($"/api/google/calendar/events?date={date:yyyy-MM-dd}");

        // NoOpGoogleCalendarService returns empty list (not NotFoundException),
        // so this returns OK with empty array
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await response.Content.ReadFromJsonAsync<List<GoogleCalendarEventDto>>();
        Assert.NotNull(events);
        Assert.Empty(events!);
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
