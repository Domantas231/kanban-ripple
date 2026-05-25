using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanban.Api.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kanban.Api.Tests.ErrorHandling;

public sealed class ErrorHandlingIntegrationTests : IClassFixture<ErrorHandlingApiFactory>
{
    private readonly ErrorHandlingApiFactory _factory;

    public ErrorHandlingIntegrationTests(ErrorHandlingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ValidationError_Returns400_WithExpectedFormat()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email",
            password = "short"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = document.RootElement.GetProperty("error");

        Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
        Assert.Equal("Validation failed.", error.GetProperty("message").GetString());
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("requestId").GetString()));
        Assert.Equal(JsonValueKind.String, error.GetProperty("timestamp").ValueKind);

        var validationErrors = error.GetProperty("validationErrors");
        Assert.Equal(JsonValueKind.Array, validationErrors.ValueKind);
        Assert.True(validationErrors.GetArrayLength() > 0);

        var first = validationErrors[0];
        Assert.True(first.TryGetProperty("propertyName", out _));
        Assert.True(first.TryGetProperty("errorMessage", out _));
        Assert.True(first.TryGetProperty("attemptedValue", out _));
    }

    [Fact]
    public async Task BadCredentials_Returns401()
    {
        using var client = CreateClient();

        var email = UniqueEmail("bad-credentials");
        const string password = "Aa1!validPassword";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task NoPermission_Returns403()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("forbidden-owner"));
        var viewerUserId = await _factory.CreateUserAsync(UniqueEmail("forbidden-viewer"));

        using var ownerClient = CreateClient(ownerUserId);
        using var viewerClient = CreateClient(viewerUserId);

        var projectId = await CreateProjectAsync(ownerClient, "Forbidden Project");
        var boardId = await CreateBoardAsync(ownerClient, projectId, "Forbidden Board");

        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = viewerUserId,
                Role = ProjectRole.Viewer,
                JoinedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        });

        var response = await viewerClient.PostAsJsonAsync($"/api/boards/{boardId}/tags", new
        {
            name = "BlockedTag",
            color = "#112233"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NotFound_Returns404()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("not-found-user"));
        using var client = CreateClient(userId);

        var response = await client.GetAsync($"/api/cards/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task VersionConflict_Returns409()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("version-owner"));
        using var client = CreateClient(ownerUserId);

        var projectId = await CreateProjectAsync(client, "Version Conflict Project");
        var boardId = await CreateBoardAsync(client, projectId, "Main Board");
        var columnId = await CreateColumnAsync(client, boardId, "Todo");
        var (cardId, version) = await CreateCardAsync(client, columnId, "Versioned Card");

        var firstUpdate = await client.PutAsJsonAsync($"/api/cards/{cardId}", new
        {
            title = "Versioned Card - Updated",
            description = "updated",
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null,
            version
        });
        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);

        var staleUpdate = await client.PutAsJsonAsync($"/api/cards/{cardId}", new
        {
            title = "Versioned Card - Stale",
            description = "stale",
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null,
            version
        });

        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
    }

    [Fact]
    public async Task RateLimitExceeded_Returns429_WithRetryAfter()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/__tests/rate-limit");

        Assert.Equal((HttpStatusCode)StatusCodes.Status429TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Retry-After", out var retryAfterValues));
        Assert.Equal("60", Assert.Single(retryAfterValues));
    }

    [Fact]
    public async Task UnhandledException_Returns500_GenericMessageWithoutStackTrace()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/__tests/unhandled");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var responseText = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseText);
        var error = document.RootElement.GetProperty("error");

        Assert.Equal("INTERNAL_SERVER_ERROR", error.GetProperty("code").GetString());
        Assert.Equal("An unexpected error occurred.", error.GetProperty("message").GetString());

        Assert.DoesNotContain("InvalidOperationException", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sensitive boom details", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stackTrace", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at ", responseText, StringComparison.Ordinal);
    }

    private HttpClient CreateClient(Guid? userId = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        if (userId.HasValue)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.Value.ToString());
        }

        return client;
    }

    private static string UniqueEmail(string prefix)
    {
        return $"{prefix}.{Guid.NewGuid():N}@example.com";
    }

    private static async Task<Guid> CreateProjectAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new { name });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateBoardAsync(HttpClient client, Guid projectId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/boards", new { name });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateColumnAsync(HttpClient client, Guid boardId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/columns", new { name });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<(Guid CardId, int Version)> CreateCardAsync(HttpClient client, Guid columnId, string title)
    {
        var response = await client.PostAsJsonAsync($"/api/columns/{columnId}/cards", new
        {
            title,
            description = "initial",
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null
        });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        return (root.GetProperty("id").GetGuid(), root.GetProperty("version").GetInt32());
    }
}

public sealed class ErrorHandlingApiFactory : KanbanApiFactoryBase
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Hosted services would interfere with the synthetic /__tests/* endpoints below.
        services.RemoveAll<IHostedService>();
        services.AddTransient<IStartupFilter, ErrorHandlingTestStartupFilter>();
    }
}

public sealed class ErrorHandlingTestStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            next(app);

            app.Map("/__tests/rate-limit", branch =>
            {
                branch.Run(async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers.RetryAfter = "60";
                });
            });

            app.Map("/__tests/unhandled", branch =>
            {
                branch.Run(_ => throw new InvalidOperationException("sensitive boom details"));
            });
        };
    }
}

