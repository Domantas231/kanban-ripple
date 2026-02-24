using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Projects;
using Kanban.Api.Tests.Projects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

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
                ProjectId = project.Id,
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
    public async Task Search_Performance_CompletesWithin500ms()
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
            $"Search request exceeded 500ms. Actual: {stopwatch.ElapsedMilliseconds}ms");
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
            plannedDurationHours = 1.0m
        });

        response.EnsureSuccessStatusCode();

        var card = await response.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(card);
        return card!;
    }
}

public sealed class PostgresCardsApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private const string DefaultAdminConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=kanban_user;Password=kanban_password";

    private readonly string _adminConnectionString;
    private readonly string _databaseName;
    private readonly string _testConnectionString;

    public PostgresCardsApiFactory()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable("KANBAN_TEST_POSTGRES_CONNECTION")
            ?? DefaultAdminConnectionString;
        _databaseName = $"kanban_cards_api_{Guid.NewGuid():N}";
        _testConnectionString = BuildDatabaseConnectionString(_adminConnectionString, _databaseName);

        EnsureDatabaseCreated();
    }

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
                ["Frontend:Url"] = "http://localhost:5173",
                ["ConnectionStrings:DefaultConnection"] = _testConnectionString
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll<ApplicationDbContext>();

            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_testConnectionString));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ProjectsApiFactory.AuthScheme;
                options.DefaultChallengeScheme = ProjectsApiFactory.AuthScheme;
                options.DefaultScheme = ProjectsApiFactory.AuthScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(ProjectsApiFactory.AuthScheme, _ => { });

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.Migrate();
        });
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

    public async Task WithDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(dbContext);
    }

    public override async ValueTask DisposeAsync()
    {
        await DropDatabaseAsync();
        await base.DisposeAsync();
    }

    private static string BuildDatabaseConnectionString(string adminConnectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = databaseName
        };

        return builder.ConnectionString;
    }

    private void EnsureDatabaseCreated()
    {
        using var adminConnection = new NpgsqlConnection(_adminConnectionString);
        adminConnection.Open();

        using var createCommand = adminConnection.CreateCommand();
        createCommand.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
        createCommand.ExecuteNonQuery();
    }

    private async Task DropDatabaseAsync()
    {
        await using var adminConnection = new NpgsqlConnection(_adminConnectionString);
        await adminConnection.OpenAsync();

        await using (var terminateCommand = adminConnection.CreateCommand())
        {
            terminateCommand.CommandText =
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @databaseName AND pid <> pg_backend_pid();";
            terminateCommand.Parameters.AddWithValue("databaseName", _databaseName);
            await terminateCommand.ExecuteNonQueryAsync();
        }

        await using (var dropCommand = adminConnection.CreateCommand())
        {
            dropCommand.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\"";
            await dropCommand.ExecuteNonQueryAsync();
        }
    }
}