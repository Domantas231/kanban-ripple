using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Projects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kanban.Api.Tests.Projects;

public sealed class ProjectEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public ProjectEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_WithValidData_ReturnsCreated()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("create-valid"));
        using var client = CreateClient(userId);

        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            name = "My Project",
            type = ProjectType.Simple
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var project = await response.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);
        Assert.Equal("My Project", project!.Name);
    }

    [Fact]
    public async Task Create_WithoutName_ReturnsBadRequest()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("create-invalid"));
        using var client = CreateClient(userId);

        using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/projects", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NonMember_AccessProject_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var create = await ownerClient.PostAsJsonAsync("/api/projects", new { name = "Private Project" });
        create.EnsureSuccessStatusCode();
        var project = await create.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var outsiderUserId = await _factory.CreateUserAsync(UniqueEmail("outsider"));
        using var outsiderClient = CreateClient(outsiderUserId);

        var response = await outsiderClient.GetAsync($"/api/projects/{project!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_UpdateProject_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("viewer-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var create = await ownerClient.PostAsJsonAsync("/api/projects", new { name = "Access Project" });
        create.EnsureSuccessStatusCode();
        var project = await create.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var viewerUserId = await _factory.CreateUserAsync(UniqueEmail("viewer"));
        using var viewerClient = CreateClient(viewerUserId);

        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project!.Id,
                UserId = viewerUserId,
                Role = ProjectRole.Viewer,
                JoinedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        var response = await viewerClient.PutAsJsonAsync($"/api/projects/{project!.Id}", new
        {
            name = "Renamed",
            type = ProjectType.Team
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Owner_UpdateProject_IgnoresTypeChange()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("update-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var create = await ownerClient.PostAsJsonAsync("/api/projects", new
        {
            name = "Update Type Guard",
            type = ProjectType.Simple
        });
        create.EnsureSuccessStatusCode();

        var project = await create.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);
        Assert.Equal(ProjectType.Simple, project!.Type);

        var update = await ownerClient.PutAsJsonAsync($"/api/projects/{project.Id}", new
        {
            name = "Renamed",
            type = ProjectType.Full
        });
        update.EnsureSuccessStatusCode();

        var updated = await update.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(updated);
        Assert.Equal("Renamed", updated!.Name);
        Assert.Equal(ProjectType.Simple, updated.Type);
    }

    [Fact]
    public async Task Member_ChangeRoles_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("roles-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var create = await ownerClient.PostAsJsonAsync("/api/projects", new { name = "Roles Project" });
        create.EnsureSuccessStatusCode();
        var project = await create.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("roles-member"));
        using var memberClient = CreateClient(memberUserId);

        var targetUserId = await _factory.CreateUserAsync(UniqueEmail("roles-target"));

        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.AddRange(
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project!.Id,
                    UserId = memberUserId,
                    Role = ProjectRole.Member,
                    JoinedAt = DateTime.UtcNow
                },
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project!.Id,
                    UserId = targetUserId,
                    Role = ProjectRole.Viewer,
                    JoinedAt = DateTime.UtcNow
                });

            await db.SaveChangesAsync();
        });

        var response = await memberClient.PutAsJsonAsync($"/api/projects/{project!.Id}/members/{targetUserId}/role", new
        {
            role = ProjectRole.Moderator
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TransferOwnership_ToNonMember_ReturnsBadRequest()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("transfer-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var create = await ownerClient.PostAsJsonAsync("/api/projects", new { name = "Ownership Project" });
        create.EnsureSuccessStatusCode();
        var project = await create.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var outsiderUserId = await _factory.CreateUserAsync(UniqueEmail("transfer-outsider"));

        var response = await ownerClient.PostAsJsonAsync($"/api/projects/{project!.Id}/transfer-ownership", new
        {
            newOwnerUserId = outsiderUserId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveRestore_RoundTrip_Works()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("archive-owner"));
        using var client = CreateClient(ownerUserId);

        var create = await client.PostAsJsonAsync("/api/projects", new { name = "Archive Project" });
        create.EnsureSuccessStatusCode();
        var project = await create.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var archive = await client.DeleteAsync($"/api/projects/{project!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var archivedGet = await client.GetAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NotFound, archivedGet.StatusCode);

        var restore = await client.PostAsync($"/api/projects/{project.Id}/restore", content: null);
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);

        var restoredGet = await client.GetAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, restoredGet.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsPaginatedResults()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("paging-owner"));
        using var client = CreateClient(ownerUserId);

        for (var i = 0; i < 30; i++)
        {
            var create = await client.PostAsJsonAsync("/api/projects", new
            {
                name = $"Project {i:00}",
                type = ProjectType.Simple
            });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        var page1 = await client.GetFromJsonAsync<PaginatedResponse<Project>>("/api/projects?page=1&pageSize=25");
        var page2 = await client.GetFromJsonAsync<PaginatedResponse<Project>>("/api/projects?page=2&pageSize=25");

        Assert.NotNull(page1);
        Assert.NotNull(page2);
        Assert.Equal(25, page1!.Items.Count);
        Assert.Equal(5, page2!.Items.Count);
        Assert.Equal(30, page1.TotalCount);
        Assert.Equal(2, page2.Page);
    }

    [Fact]
    public async Task GetSwimlane_ReturnsNestedBoardsColumnsCardsAndCounts()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("swimlane-owner"));
        using var client = CreateClient(ownerUserId);

        var create = await client.PostAsJsonAsync("/api/projects", new { name = "Swimlane Project" });
        create.EnsureSuccessStatusCode();

        var project = await create.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var activeBoardId = Guid.NewGuid();
        var archivedBoardId = Guid.NewGuid();
        var firstColumnId = Guid.NewGuid();
        var secondColumnId = Guid.NewGuid();
        var archivedColumnId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await _factory.WithDbContextAsync(async db =>
        {
            db.Boards.AddRange(
                new Board
                {
                    Id = activeBoardId,
                    ProjectId = project!.Id,
                    Name = "Active Board",
                    Position = 1000,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Board
                {
                    Id = archivedBoardId,
                    ProjectId = project!.Id,
                    Name = "Archived Board",
                    Position = 2000,
                    CreatedAt = now,
                    UpdatedAt = now,
                    DeletedAt = now
                });

            db.Columns.AddRange(
                new Column
                {
                    Id = firstColumnId,
                    BoardId = activeBoardId,
                    Name = "Todo",
                    Position = 1000,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Column
                {
                    Id = secondColumnId,
                    BoardId = activeBoardId,
                    Name = "Doing",
                    Position = 2000,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Column
                {
                    Id = archivedColumnId,
                    BoardId = activeBoardId,
                    Name = "Archived Column",
                    Position = 3000,
                    CreatedAt = now,
                    UpdatedAt = now,
                    DeletedAt = now
                });

            db.Cards.AddRange(
                new Card
                {
                    Id = Guid.NewGuid(),
                    ColumnId = firstColumnId,
                    Title = "Todo A",
                    Position = 1000,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Card
                {
                    Id = Guid.NewGuid(),
                    ColumnId = firstColumnId,
                    Title = "Todo B",
                    Position = 2000,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Card
                {
                    Id = Guid.NewGuid(),
                    ColumnId = secondColumnId,
                    Title = "Doing A",
                    Position = 1000,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Card
                {
                    Id = Guid.NewGuid(),
                    ColumnId = secondColumnId,
                    Title = "Doing Archived",
                    Position = 2000,
                    CreatedAt = now,
                    UpdatedAt = now,
                    DeletedAt = now
                },
                new Card
                {
                    Id = Guid.NewGuid(),
                    ColumnId = archivedColumnId,
                    Title = "Archived Column Card",
                    Position = 1000,
                    CreatedAt = now,
                    UpdatedAt = now
                });

            await db.SaveChangesAsync();
        });

        var response = await client.GetAsync($"/api/projects/{project!.Id}/swimlane");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var swimlane = await response.Content.ReadFromJsonAsync<SwimlaneView>();
        Assert.NotNull(swimlane);
        Assert.Equal(project.Id, swimlane!.ProjectId);

        Assert.Single(swimlane.Boards);
        var board = swimlane.Boards[0];
        Assert.Equal(activeBoardId, board.Board.Id);
        Assert.Equal("Active Board", board.Board.Name);

        Assert.Equal(2, board.Columns.Count);
        Assert.Equal(new[] { firstColumnId, secondColumnId }, board.Columns.Select(x => x.Column.Id));

        var todo = board.Columns[0];
        Assert.Equal(2, todo.CardCount);
        Assert.Equal(2, todo.Cards.Count);
        Assert.Equal(new[] { "Todo A", "Todo B" }, todo.Cards.Select(x => x.Title));

        var doing = board.Columns[1];
        Assert.Equal(1, doing.CardCount);
        Assert.Single(doing.Cards);
        Assert.Equal("Doing A", doing.Cards[0].Title);
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
        return $"{prefix}.{Guid.NewGuid():N}@example.com";
    }

}

public sealed class ProjectsApiFactory : WebApplicationFactory<Program>
{
    public const string AuthScheme = "Test";
    private string _databaseName = $"project-endpoints-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "Kanban.Tests",
                ["Jwt:Audience"] = "Kanban.Tests.Client",
                ["Jwt:Key"] = "super_secret_test_key_12345678901234567890",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Frontend:Url"] = "http://localhost:5173"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll<ApplicationDbContext>();

            _databaseName = $"project-endpoints-{Guid.NewGuid():N}";
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthScheme;
                options.DefaultChallengeScheme = AuthScheme;
                options.DefaultScheme = AuthScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(AuthScheme, _ => { });

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }

    public async Task WithDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(dbContext);
    }

    public async Task<Guid> CreateUserAsync(string email)
    {
        var userId = Guid.NewGuid();

        await WithDbContextAsync(async dbContext =>
        {
            dbContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                Email = email,
                UserName = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant(),
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString("N")
            });

            await dbContext.SaveChangesAsync();
        });

        return userId;
    }
}

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string UserIdHeaderName = "X-Test-UserId";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeaderName, out var userIdValues)
            || !Guid.TryParse(userIdValues.ToString(), out var userId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing or invalid test user id."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
