using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Services.Planner;
using Kanban.Api.Tests.Projects;

namespace Kanban.Api.Tests.Planner;

public sealed class PlannerEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public PlannerEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateBlock_ValidRequest_ReturnsOk()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("planner-create"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Planner Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Task 1");

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
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
        Assert.Equal(card.Id, block!.CardId);
        Assert.Equal(card.Title, block.CardTitle);
        Assert.Equal(date, block.Date);
        Assert.Equal(new TimeOnly(9, 0), block.StartTime);
        Assert.Equal(new TimeOnly(10, 0), block.EndTime);
        Assert.Equal(PlannedBlockSyncStatus.Synced, block.SyncStatus);
    }

    [Fact]
    public async Task CreateBlock_CardFromDifferentProject_ReturnsBadRequest()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("planner-diff-proj"));
        using var client = CreateClient(userId);

        var project1 = await CreateProjectAsync(client, "Project 1");
        var project2 = await CreateProjectAsync(client, "Project 2");
        var board = await CreateBoardAsync(client, project1.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card in Project 1");

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var response = await client.PostAsJsonAsync($"/api/projects/{project2.Id}/planner/blocks", new
        {
            cardId = card.Id,
            date = date.ToString("yyyy-MM-dd"),
            startTime = "09:00:00",
            endTime = "10:00:00"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBlock_SameCardDifferentTimesOnSameDate_ReturnsOk()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("planner-multi"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Multi Block Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Repeating Card");

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var firstResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/planner/blocks", new
        {
            cardId = card.Id,
            date = date.ToString("yyyy-MM-dd"),
            startTime = "09:00:00",
            endTime = "10:00:00"
        });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/planner/blocks", new
        {
            cardId = card.Id,
            date = date.ToString("yyyy-MM-dd"),
            startTime = "11:00:00",
            endTime = "12:00:00"
        });
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var listResponse = await client.GetAsync(
            $"/api/projects/{project.Id}/planner/blocks?date={date:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var blocks = await listResponse.Content.ReadFromJsonAsync<List<PlannedBlockDto>>();
        Assert.NotNull(blocks);
        Assert.Equal(2, blocks!.Count(b => b.CardId == card.Id));
    }

    [Fact]
    public async Task GetBlocks_ReturnsBlocksForDate()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("planner-get"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Get Blocks Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card1 = await CreateCardAsync(client, column.Id, "Card 1");
        var card2 = await CreateCardAsync(client, column.Id, "Card 2");

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        await client.PostAsJsonAsync($"/api/projects/{project.Id}/planner/blocks", new
        {
            cardId = card1.Id,
            date = date.ToString("yyyy-MM-dd"),
            startTime = "09:00:00",
            endTime = "10:00:00"
        });
        await client.PostAsJsonAsync($"/api/projects/{project.Id}/planner/blocks", new
        {
            cardId = card2.Id,
            date = date.ToString("yyyy-MM-dd"),
            startTime = "10:00:00",
            endTime = "11:00:00"
        });

        var response = await client.GetAsync($"/api/projects/{project.Id}/planner/blocks?date={date:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var blocks = await response.Content.ReadFromJsonAsync<List<PlannedBlockDto>>();
        Assert.NotNull(blocks);
        Assert.Equal(2, blocks!.Count);
        Assert.Equal("Card 1", blocks[0].CardTitle);
        Assert.Equal("Card 2", blocks[1].CardTitle);
    }

    [Fact]
    public async Task UpdateBlock_ChangesTimeRange()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("planner-update"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Update Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Update Card");

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
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
        Assert.Equal(new TimeOnly(14, 0), updated!.StartTime);
        Assert.Equal(new TimeOnly(16, 0), updated.EndTime);
    }

    [Fact]
    public async Task DeleteBlock_RemovesBlock()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("planner-delete"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Delete Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Delete Card");

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4));
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/planner/blocks", new
        {
            cardId = card.Id,
            date = date.ToString("yyyy-MM-dd"),
            startTime = "09:00:00",
            endTime = "10:00:00"
        });
        var block = await createResponse.Content.ReadFromJsonAsync<PlannedBlockDto>();

        var deleteResponse = await client.DeleteAsync(
            $"/api/projects/{project.Id}/planner/blocks/{block!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/projects/{project.Id}/planner/blocks?date={date:yyyy-MM-dd}");
        var blocks = await getResponse.Content.ReadFromJsonAsync<List<PlannedBlockDto>>();
        Assert.NotNull(blocks);
        Assert.Empty(blocks!);
    }

    [Fact]
    public async Task DeleteBlock_NotOwner_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("planner-del-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Owner Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");
        var card = await CreateCardAsync(ownerClient, column.Id, "Owner Card");

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var createResponse = await ownerClient.PostAsJsonAsync($"/api/projects/{project.Id}/planner/blocks", new
        {
            cardId = card.Id,
            date = date.ToString("yyyy-MM-dd"),
            startTime = "09:00:00",
            endTime = "10:00:00"
        });
        var block = await createResponse.Content.ReadFromJsonAsync<PlannedBlockDto>();

        var otherUserId = await _factory.CreateUserAsync(UniqueEmail("planner-del-other"));
        using var otherClient = CreateClient(otherUserId);

        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = otherUserId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        var deleteResponse = await otherClient.DeleteAsync(
            $"/api/projects/{project.Id}/planner/blocks/{block!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task GetUnscheduledCards_ExcludesScheduledCards()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("planner-unsched"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Unsched Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card1 = await CreateCardAsync(client, column.Id, "Scheduled Card");
        var card2 = await CreateCardAsync(client, column.Id, "Unscheduled Card");

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6));
        await client.PostAsJsonAsync($"/api/projects/{project.Id}/planner/blocks", new
        {
            cardId = card1.Id,
            date = date.ToString("yyyy-MM-dd"),
            startTime = "09:00:00",
            endTime = "10:00:00"
        });

        var response = await client.GetAsync(
            $"/api/projects/{project.Id}/planner/unscheduled?date={date:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cards = await response.Content.ReadFromJsonAsync<List<UnscheduledCardDto>>();
        Assert.NotNull(cards);
        Assert.DoesNotContain(cards!, c => c.Id == card1.Id);
        Assert.Contains(cards!, c => c.Id == card2.Id);
    }

    [Fact]
    public async Task NonMember_CannotCreateBlock()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("planner-nonmem-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "NonMember Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");
        var card = await CreateCardAsync(ownerClient, column.Id, "Card");

        var nonMemberUserId = await _factory.CreateUserAsync(UniqueEmail("planner-nonmem"));
        using var nonMemberClient = CreateClient(nonMemberUserId);

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var response = await nonMemberClient.PostAsJsonAsync($"/api/projects/{project.Id}/planner/blocks", new
        {
            cardId = card.Id,
            date = date.ToString("yyyy-MM-dd"),
            startTime = "09:00:00",
            endTime = "10:00:00"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
