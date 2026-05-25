using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Kanban.Api.Tests.Projects;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace Kanban.Api.Tests.Realtime;

public sealed class RealtimeSyncPropertyTests : IClassFixture<ProjectsApiFactory>
{
    private static readonly TimeSpan RealtimeTimeout = TimeSpan.FromSeconds(2);

    private readonly ProjectsApiFactory _factory;

    public RealtimeSyncPropertyTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Property_77_ChangeByOneUser_ReachesOtherConnectedUsersWithinTwoSeconds()
    {
        var actorUserId = await _factory.CreateUserAsync(UniqueEmail("property-77-actor"));
        using var actorClient = CreateClient(actorUserId);

        var project = await CreateProjectAsync(actorClient, "Realtime Property 77 Project");
        var board = await CreateBoardAsync(actorClient, project.Id, "Main Board");
        var column = await CreateColumnAsync(actorClient, board.Id, "Todo");
        var card = await CreateCardAsync(actorClient, column.Id, "Realtime Card");

        var observerUserId = await _factory.CreateUserAsync(UniqueEmail("property-77-observer"));
        await AddProjectMemberAsync(project.Id, observerUserId, ProjectRole.Member);

        var deleteEvents = Channel.CreateUnbounded<Guid>();
        await using var observerConnection = CreateHubConnection(observerUserId);
        observerConnection.On<Guid>(nameof(IProjectClient.CardDeleted), deletedCardId =>
        {
            deleteEvents.Writer.TryWrite(deletedCardId);
        });

        await observerConnection.StartAsync();
        await observerConnection.InvokeAsync(nameof(ProjectHub.JoinProject), project.Id);

        var stopwatch = Stopwatch.StartNew();
        var deleteResponse = await actorClient.DeleteAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var receivedCardId = await ReadWithTimeoutAsync(deleteEvents.Reader, RealtimeTimeout);
        stopwatch.Stop();

        Assert.NotEqual(Guid.Empty, receivedCardId);
        Assert.Equal(card.Id, receivedCardId);
        Assert.True(
            stopwatch.Elapsed <= RealtimeTimeout,
            $"Expected SignalR update broadcast within {RealtimeTimeout.TotalSeconds:0} seconds but took {stopwatch.Elapsed.TotalSeconds:F3} seconds.");

        await observerConnection.InvokeAsync(nameof(ProjectHub.LeaveProject), project.Id);
        await observerConnection.StopAsync();
    }

    [Fact]
    public async Task Property_78_WrongVersionUpdateReturnsConflict_AndMoveUsesLastWriteWinsWithoutVersionCheck()
    {
        var actorUserId = await _factory.CreateUserAsync(UniqueEmail("property-78-actor"));
        using var actorClient = CreateClient(actorUserId);

        var project = await CreateProjectAsync(actorClient, "Realtime Property 78 Project");
        var board = await CreateBoardAsync(actorClient, project.Id, "Main Board");
        var sourceColumn = await CreateColumnAsync(actorClient, board.Id, "Source");
        var firstTargetColumn = await CreateColumnAsync(actorClient, board.Id, "Target A");
        var secondTargetColumn = await CreateColumnAsync(actorClient, board.Id, "Target B");
        var card = await CreateCardAsync(actorClient, sourceColumn.Id, "Versioned Realtime Card");

        var validUpdateResponse = await actorClient.PutAsJsonAsync($"/api/cards/{card.Id}", new
        {
            title = "Versioned Realtime Card V2",
            description = "v2",
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null,
            version = card.Version
        });
        Assert.Equal(HttpStatusCode.OK, validUpdateResponse.StatusCode);

        var staleUpdateResponse = await actorClient.PutAsJsonAsync($"/api/cards/{card.Id}", new
        {
            title = "Versioned Realtime Card Stale",
            description = "stale",
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null,
            version = card.Version
        });
        Assert.Equal(HttpStatusCode.Conflict, staleUpdateResponse.StatusCode);

        var firstMoveResponse = await actorClient.PutAsJsonAsync($"/api/cards/{card.Id}/move", new
        {
            columnId = firstTargetColumn.Id,
            position = 0
        });
        Assert.Equal(HttpStatusCode.OK, firstMoveResponse.StatusCode);

        var secondMoveResponse = await actorClient.PutAsJsonAsync($"/api/cards/{card.Id}/move", new
        {
            columnId = secondTargetColumn.Id,
            position = 0
        });
        Assert.Equal(HttpStatusCode.OK, secondMoveResponse.StatusCode);

        var getMovedResponse = await actorClient.GetAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.OK, getMovedResponse.StatusCode);

        var movedCard = await getMovedResponse.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(movedCard);
        Assert.Equal(secondTargetColumn.Id, movedCard!.ColumnId);
    }

    private HubConnection CreateHubConnection(Guid userId)
    {
        return new HubConnectionBuilder()
            .WithUrl("https://localhost/hubs/project", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());
            })
            .Build();
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

    private async Task AddProjectMemberAsync(Guid projectId, Guid userId, ProjectRole role)
    {
        await _factory.WithDbContextAsync(async dbContext =>
        {
            dbContext.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = userId,
                Role = role,
                JoinedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
        });
    }

    private static async Task<T?> ReadWithTimeoutAsync<T>(ChannelReader<T> reader, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            return await reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return default;
        }
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
