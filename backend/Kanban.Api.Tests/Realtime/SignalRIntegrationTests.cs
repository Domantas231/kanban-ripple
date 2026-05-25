using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Channels;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Kanban.Api.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Realtime;

public sealed class SignalRIntegrationTests : IClassFixture<JwtProjectsApiFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(2);

    private readonly JwtProjectsApiFactory _factory;

    public SignalRIntegrationTests(JwtProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Connect_WithValidJwt_Succeeds()
    {
        var actor = await RegisterAsync("signalr-connect-actor");

        await using var connection = CreateHubConnection(actor.AccessToken);

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);

        await connection.StopAsync();
    }

    [Fact]
    public async Task JoinProject_AsNonMember_ThrowsHubException()
    {
        var owner = await RegisterAsync("signalr-join-owner");
        using var ownerClient = CreateApiClient(owner.AccessToken);

        var project = await CreateProjectAsync(ownerClient, "SignalR Join Access Project");

        var outsider = await RegisterAsync("signalr-join-outsider");
        await using var outsiderConnection = CreateHubConnection(outsider.AccessToken);

        await outsiderConnection.StartAsync();

        var exception = await Assert.ThrowsAsync<HubException>(() =>
            outsiderConnection.InvokeAsync(nameof(ProjectHub.JoinProject), project.Id));

        Assert.Contains("Access denied", exception.Message, StringComparison.OrdinalIgnoreCase);

        await outsiderConnection.StopAsync();
    }

    [Fact]
    public async Task CreateCard_BroadcastsCardCreated_ToOtherConnectedMemberWithinTwoSeconds()
    {
        var owner = await RegisterAsync("signalr-cardcreated-owner");
        using var ownerClient = CreateApiClient(owner.AccessToken);

        var project = await CreateProjectAsync(ownerClient, "SignalR CardCreated Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Main Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");

        var observer = await RegisterAsync("signalr-cardcreated-observer");
        await AddProjectMemberAsync(project.Id, observer.UserId, ProjectRole.Member);

        var createdEvents = Channel.CreateUnbounded<Card>();
        await using var observerConnection = CreateHubConnection(observer.AccessToken);

        observerConnection.On<Card>(nameof(IProjectClient.CardCreated), card =>
        {
            createdEvents.Writer.TryWrite(card);
        });

        await observerConnection.StartAsync();
        await observerConnection.InvokeAsync(nameof(ProjectHub.JoinProject), project.Id);

        var stopwatch = Stopwatch.StartNew();
        var createdCard = await CreateCardAsync(ownerClient, column.Id, "Realtime Created Card");

        var receivedCard = await ReadWithTimeoutAsync(createdEvents.Reader, EventTimeout);
        stopwatch.Stop();

        Assert.NotNull(receivedCard);
        Assert.Equal(createdCard.Id, receivedCard!.Id);
        Assert.True(
            stopwatch.Elapsed <= EventTimeout,
            $"Expected CardCreated broadcast within {EventTimeout.TotalSeconds:0} seconds but took {stopwatch.Elapsed.TotalSeconds:F3} seconds.");

        await observerConnection.InvokeAsync(nameof(ProjectHub.LeaveProject), project.Id);
        await observerConnection.StopAsync();
    }

    private HubConnection CreateHubConnection(string accessToken)
    {
        return new HubConnectionBuilder()
            .WithUrl("https://localhost/hubs/project", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();
    }

    private HttpClient CreateApiClient(string accessToken)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
        return client;
    }

    private async Task<TestUserSession> RegisterAsync(string prefix)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var email = UniqueEmail(prefix);
        const string password = "Password123!";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password
        });
        registerResponse.EnsureSuccessStatusCode();

        // Register returns only a confirmation message; mark the email confirmed manually so login works.
        await _factory.WithDbContextAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.EmailConfirmed = true;
            await db.SaveChangesAsync();
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();

        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResult>();
        Assert.NotNull(authResult);

        return new TestUserSession(authResult!.UserId, authResult.AccessToken);
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

    private sealed record TestUserSession(Guid UserId, string AccessToken);
}

/// <summary>
/// Factory for SignalR integration tests. Keeps the real JWT pipeline (no
/// <see cref="TestAuthHandler"/>) so connection auth exercises the production code path.
/// </summary>
public sealed class JwtProjectsApiFactory : KanbanApiFactoryBase
{
    protected override bool UseTestAuthHandler => false;
}
