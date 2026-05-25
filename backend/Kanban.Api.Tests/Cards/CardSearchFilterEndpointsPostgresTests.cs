using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Services.Projects;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kanban.Api.Tests.Cards;

public sealed class CardSearchFilterEndpointsPostgresTests : IClassFixture<PostgresCardsApiFactory>
{
    private readonly PostgresCardsApiFactory _factory;

    public CardSearchFilterEndpointsPostgresTests(PostgresCardsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Search_SubstringCaseInsensitive_MatchesKanban()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("pg-search-substring"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "PG Search Substring Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");

        var match = await CreateCardAsync(client, column.Id, "Kanban planning");
        await CreateCardAsync(client, column.Id, "Roadmap");

        var response = await client.GetAsync($"/api/projects/{project.Id}/cards/search?q=KAN");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PaginatedResponse<Card>>();
        Assert.NotNull(payload);
        Assert.Contains(payload!.Items, x => x.Id == match.Id);
    }

    [Fact]
    public async Task Filter_CombinedTagAndUser_ReturnsIntersection()
    {
        var ownerId = await _factory.CreateUserAsync(UniqueEmail("pg-filter-owner"));
        using var ownerClient = CreateClient(ownerId);

        var project = await CreateProjectAsync(ownerClient, "PG Filter Combined Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Main Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");

        var assigneeId = await _factory.CreateUserAsync(UniqueEmail("pg-filter-assignee"));
        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = assigneeId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        });

        var targetCard = await CreateCardAsync(ownerClient, column.Id, "Card with tag + user");
        var tagOnlyCard = await CreateCardAsync(ownerClient, column.Id, "Card with tag only");
        var userOnlyCard = await CreateCardAsync(ownerClient, column.Id, "Card with user only");
        await CreateCardAsync(ownerClient, column.Id, "Card with neither");

        var tagId = Guid.NewGuid();
        await _factory.WithDbContextAsync(async db =>
        {
            db.Tags.Add(new Tag
            {
                Id = tagId,
                BoardId = board.Id,
                Name = "Urgent",
                Color = "#FF5733",
                CreatedAt = DateTime.UtcNow
            });

            db.CardTags.AddRange(
                new CardTag
                {
                    Id = Guid.NewGuid(),
                    CardId = targetCard.Id,
                    TagId = tagId,
                    CreatedAt = DateTime.UtcNow
                },
                new CardTag
                {
                    Id = Guid.NewGuid(),
                    CardId = tagOnlyCard.Id,
                    TagId = tagId,
                    CreatedAt = DateTime.UtcNow
                });

            db.CardAssignments.AddRange(
                new CardAssignment
                {
                    Id = Guid.NewGuid(),
                    CardId = targetCard.Id,
                    UserId = assigneeId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = ownerId
                },
                new CardAssignment
                {
                    Id = Guid.NewGuid(),
                    CardId = userOnlyCard.Id,
                    UserId = assigneeId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedBy = ownerId
                });

            await db.SaveChangesAsync();
        });

        var response = await ownerClient.GetAsync(
            $"/api/boards/{board.Id}/cards/filter?tagIds={tagId}&userIds={assigneeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<Card>>();
        Assert.NotNull(payload);

        var single = Assert.Single(payload!);
        Assert.Equal(targetCard.Id, single.Id);
    }

    [Fact]
    public async Task Search_Pagination_Works()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("pg-search-pagination"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "PG Search Pagination Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");

        for (var i = 0; i < 5; i++)
        {
            await CreateCardAsync(client, column.Id, $"Kanban task {i}");
            await Task.Delay(5);
        }

        var pageOneResponse = await client.GetAsync(
            $"/api/projects/{project.Id}/cards/search?q=kan&page=1&pageSize=2");
        var pageTwoResponse = await client.GetAsync(
            $"/api/projects/{project.Id}/cards/search?q=kan&page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, pageOneResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pageTwoResponse.StatusCode);

        var pageOne = await pageOneResponse.Content.ReadFromJsonAsync<PaginatedResponse<Card>>();
        var pageTwo = await pageTwoResponse.Content.ReadFromJsonAsync<PaginatedResponse<Card>>();

        Assert.NotNull(pageOne);
        Assert.NotNull(pageTwo);

        Assert.Equal(5, pageOne!.TotalCount);
        Assert.Equal(2, pageOne.PageSize);
        Assert.Equal(1, pageOne.Page);
        Assert.Equal(2, pageOne.Items.Count);

        Assert.Equal(5, pageTwo!.TotalCount);
        Assert.Equal(2, pageTwo.PageSize);
        Assert.Equal(2, pageTwo.Page);
        Assert.Equal(2, pageTwo.Items.Count);

        var overlap = pageOne.Items.Select(x => x.Id).Intersect(pageTwo.Items.Select(x => x.Id));
        Assert.Empty(overlap);
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsBadRequest()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("pg-search-empty"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "PG Search Empty Project");

        var response = await client.GetAsync($"/api/projects/{project.Id}/cards/search");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Property_82_SearchPerformance_SeededResultsReturnWithin500ms()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("pg-search-perf"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "PG Search Performance Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");

        for (var i = 0; i < 300; i++)
        {
            var title = i % 3 == 0 ? $"Kanban benchmark task {i}" : $"Regular task {i}";
            await CreateCardAsync(client, column.Id, title);
        }

        var warmup = await client.GetAsync($"/api/projects/{project.Id}/cards/search?q=kan&page=1&pageSize=25");
        Assert.Equal(HttpStatusCode.OK, warmup.StatusCode);

        var stopwatch = Stopwatch.StartNew();
        var measured = await client.GetAsync($"/api/projects/{project.Id}/cards/search?q=kan&page=1&pageSize=25");
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, measured.StatusCode);
        Assert.True(
            stopwatch.ElapsedMilliseconds <= 500,
            $"Property 82 violated: search request exceeded 500ms. Actual: {stopwatch.ElapsedMilliseconds}ms");
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());
        return client;
    }

    private static string UniqueEmail(string prefix)
    {
        return $"{prefix}.{Guid.NewGuid():N}@example.test";
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
            description = "integration test card",
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null
        });

        response.EnsureSuccessStatusCode();

        var card = await response.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(card);
        return card!;
    }
}

/// <summary>
/// Postgres-only factory for trigram search tests. Identical to <see cref="ProjectsApiFactory"/> —
/// the shared base already uses the Testcontainers Postgres instance so no extra setup is needed.
/// </summary>
public sealed class PostgresCardsApiFactory : KanbanApiFactoryBase
{
}