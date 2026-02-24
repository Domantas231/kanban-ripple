using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Boards;
using Kanban.Api.Services.Cards;
using Kanban.Api.Services.Columns;
using Kanban.Api.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kanban.Api.Tests.Cards;

public sealed class CardSearchPropertyTests
{
    [Fact]
    public async Task Property_52_SearchCompleteness_SubstringCaseInsensitiveScopedAndExcludesArchived()
    {
        await using var fixture = await PostgresSearchFixture.CreateAsync();

        var ownerId = await fixture.CreateUserAsync("search-owner");

        var projectA = await fixture.ProjectService.CreateAsync(ownerId, "Search Project A", ProjectType.Team);
        var boardA = await fixture.BoardService.CreateAsync(projectA.Id, ownerId, "Board A");
        var columnA = await fixture.ColumnService.CreateAsync(boardA.Id, ownerId, "Todo A");

        var projectB = await fixture.ProjectService.CreateAsync(ownerId, "Search Project B", ProjectType.Team);
        var boardB = await fixture.BoardService.CreateAsync(projectB.Id, ownerId, "Board B");
        var columnB = await fixture.ColumnService.CreateAsync(boardB.Id, ownerId, "Todo B");

        var matchByTitle = await fixture.CardService.CreateAsync(
            columnA.Id,
            ownerId,
            new CreateCardDto("Kanban implementation", "non-matching description", null));

        var matchByDescription = await fixture.CardService.CreateAsync(
            columnA.Id,
            ownerId,
            new CreateCardDto("Search docs", "Design notes for KANBAN search", null));

        var nonMatchInScope = await fixture.CardService.CreateAsync(
            columnA.Id,
            ownerId,
            new CreateCardDto("Sprint planning", "no keyword here", null));

        var outOfScopeMatch = await fixture.CardService.CreateAsync(
            columnB.Id,
            ownerId,
            new CreateCardDto("Kanban in other project", "should not be returned", null));

        var archivedMatch = await fixture.CardService.CreateAsync(
            columnA.Id,
            ownerId,
            new CreateCardDto("Archived Kanban Card", "should be filtered", null));

        await fixture.CardService.ArchiveAsync(archivedMatch.Id, ownerId);

        var result = await fixture.CardService.SearchAsync(projectA.Id, ownerId, "kAn", page: 1, pageSize: 25);

        var resultIds = result.Items.Select(x => x.Id).ToHashSet();

        Assert.Contains(matchByTitle.Id, resultIds);
        Assert.Contains(matchByDescription.Id, resultIds);
        Assert.DoesNotContain(nonMatchInScope.Id, resultIds);
        Assert.DoesNotContain(outOfScopeMatch.Id, resultIds);
        Assert.DoesNotContain(archivedMatch.Id, resultIds);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, card =>
        {
            var title = card.Title ?? string.Empty;
            var description = card.Description ?? string.Empty;
            Assert.True(
                title.Contains("kan", StringComparison.OrdinalIgnoreCase)
                || description.Contains("kan", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public async Task Property_53_SearchResultsIncludeTitleDescriptionPreviewAndLocationMetadata()
    {
        await using var fixture = await PostgresSearchFixture.CreateAsync();

        var ownerId = await fixture.CreateUserAsync("search-metadata-owner");
        var project = await fixture.ProjectService.CreateAsync(ownerId, "Search Metadata Project", ProjectType.Team);
        var board = await fixture.BoardService.CreateAsync(project.Id, ownerId, "Delivery Board");
        var column = await fixture.ColumnService.CreateAsync(board.Id, ownerId, "In Progress");

        var card = await fixture.CardService.CreateAsync(
            column.Id,
            ownerId,
            new CreateCardDto(
                "Kanban metadata card",
                "This is a preview-ready description for search metadata verification.",
                null));

        var result = await fixture.CardService.SearchAsync(project.Id, ownerId, "kan", page: 1, pageSize: 25);
        var found = Assert.Single(result.Items, x => x.Id == card.Id);

        Assert.False(string.IsNullOrWhiteSpace(found.Title));
        Assert.False(string.IsNullOrWhiteSpace(found.Description));

        Assert.Equal(column.Id, found.ColumnId);
        Assert.NotNull(found.Column);
        Assert.Equal("In Progress", found.Column.Name);

        Assert.NotNull(found.Column.Board);
        Assert.Equal(board.Id, found.Column.Board.Id);
        Assert.Equal("Delivery Board", found.Column.Board.Name);
    }

    private sealed class PostgresSearchFixture : IAsyncDisposable
    {
        private const string DefaultAdminConnectionString =
            "Host=localhost;Port=5432;Database=postgres;Username=kanban_user;Password=kanban_password";

        private readonly string _adminConnectionString;
        private readonly string _databaseName;

        private PostgresSearchFixture(
            string adminConnectionString,
            string databaseName,
            ApplicationDbContext dbContext,
            ProjectService projectService,
            BoardService boardService,
            ColumnService columnService,
            CardService cardService)
        {
            _adminConnectionString = adminConnectionString;
            _databaseName = databaseName;
            DbContext = dbContext;
            ProjectService = projectService;
            BoardService = boardService;
            ColumnService = columnService;
            CardService = cardService;
        }

        public ApplicationDbContext DbContext { get; }
        public ProjectService ProjectService { get; }
        public BoardService BoardService { get; }
        public ColumnService ColumnService { get; }
        public CardService CardService { get; }

        public static async Task<PostgresSearchFixture> CreateAsync()
        {
            var adminConnectionString = Environment.GetEnvironmentVariable("KANBAN_TEST_POSTGRES_CONNECTION")
                ?? DefaultAdminConnectionString;
            var databaseName = $"kanban_search_prop_{Guid.NewGuid():N}";

            await using (var adminConnection = new NpgsqlConnection(adminConnectionString))
            {
                await adminConnection.OpenAsync();
                await using var createCommand = adminConnection.CreateCommand();
                createCommand.CommandText = $"CREATE DATABASE \"{databaseName}\"";
                await createCommand.ExecuteNonQueryAsync();
            }

            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Database = databaseName
            };

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionStringBuilder.ConnectionString)
                .Options;

            var dbContext = new ApplicationDbContext(options);
            await dbContext.Database.MigrateAsync();

            var projectService = new ProjectService(dbContext);
            var boardService = new BoardService(dbContext);
            var columnService = new ColumnService(dbContext);
            var cardService = new CardService(dbContext);

            return new PostgresSearchFixture(
                adminConnectionString,
                databaseName,
                dbContext,
                projectService,
                boardService,
                columnService,
                cardService);
        }

        public async Task<Guid> CreateUserAsync(string emailPrefix)
        {
            var userId = Guid.NewGuid();
            var email = $"{emailPrefix}.{Guid.NewGuid():N}@example.test";

            DbContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                Email = email,
                UserName = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant(),
                SecurityStamp = Guid.NewGuid().ToString("N"),
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await DbContext.SaveChangesAsync();
            return userId;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();

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
}