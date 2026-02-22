using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Tests.Projects;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Boards;

public sealed class BoardColumnEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public BoardColumnEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ArchiveBoard_CascadesToColumnsAndCards()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("archive-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Archive Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var firstColumn = await CreateColumnAsync(client, board.Id, "Todo");
        var secondColumn = await CreateColumnAsync(client, board.Id, "Done");

        var firstCardId = await SeedCardAsync(firstColumn.Id, "Card A");
        var secondCardId = await SeedCardAsync(secondColumn.Id, "Card B");

        var archiveResponse = await client.DeleteAsync($"/api/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        var getArchivedBoard = await client.GetAsync($"/api/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getArchivedBoard.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var archivedBoard = await db.Boards
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == board.Id);
            Assert.NotNull(archivedBoard.DeletedAt);

            var archivedColumns = await db.Columns
                .IgnoreQueryFilters()
                .Where(x => x.BoardId == board.Id)
                .ToListAsync();
            Assert.Equal(2, archivedColumns.Count);
            Assert.All(archivedColumns, column => Assert.NotNull(column.DeletedAt));

            var archivedCards = await db.Cards
                .IgnoreQueryFilters()
                .Where(x => x.Id == firstCardId || x.Id == secondCardId)
                .ToListAsync();
            Assert.Equal(2, archivedCards.Count);
            Assert.All(archivedCards, card => Assert.NotNull(card.DeletedAt));
        });
    }

    [Fact]
    public async Task RestoreBoard_RestoresBoardColumnsAndCards()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("restore-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Restore Project");
        var board = await CreateBoardAsync(client, project.Id, "Restore Board");
        var column = await CreateColumnAsync(client, board.Id, "In Progress");
        var cardId = await SeedCardAsync(column.Id, "Restore Card");

        var archiveResponse = await client.DeleteAsync($"/api/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        var restoreResponse = await client.PostAsync($"/api/boards/{board.Id}/restore", content: null);
        Assert.Equal(HttpStatusCode.NoContent, restoreResponse.StatusCode);

        var getRestoredBoard = await client.GetAsync($"/api/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.OK, getRestoredBoard.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var restoredBoard = await db.Boards
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == board.Id);
            Assert.Null(restoredBoard.DeletedAt);

            var restoredColumn = await db.Columns
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == column.Id);
            Assert.Null(restoredColumn.DeletedAt);

            var restoredCard = await db.Cards
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == cardId);
            Assert.Null(restoredCard.DeletedAt);
        });
    }

    [Fact]
    public async Task ReorderColumns_UpdatesPositionCorrectly()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("reorder-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Reorder Project");
        var board = await CreateBoardAsync(client, project.Id, "Reorder Board");
        var first = await CreateColumnAsync(client, board.Id, "First");
        var second = await CreateColumnAsync(client, board.Id, "Second");
        var third = await CreateColumnAsync(client, board.Id, "Third");

        var reorderResponse = await client.PutAsJsonAsync($"/api/columns/{third.Id}/reorder", new
        {
            beforeColumnId = first.Id,
            afterColumnId = second.Id
        });

        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);
        var reordered = await reorderResponse.Content.ReadFromJsonAsync<Column>();
        Assert.NotNull(reordered);
        Assert.Equal(1500, reordered!.Position);

        var listedColumns = await client.GetFromJsonAsync<List<Column>>($"/api/boards/{board.Id}/columns");
        Assert.NotNull(listedColumns);

        Assert.Equal(new[] { first.Id, third.Id, second.Id }, listedColumns!.Select(x => x.Id));
        Assert.Equal(new[] { 1000, 1500, 2000 }, listedColumns.Select(x => x.Position));
    }

    [Fact]
    public async Task Viewer_CreateBoardAndColumn_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("viewer-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Viewer Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Viewer Board");

        var viewerUserId = await _factory.CreateUserAsync(UniqueEmail("viewer-user"));
        using var viewerClient = CreateClient(viewerUserId);

        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = viewerUserId,
                Role = ProjectRole.Viewer,
                JoinedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        });

        var createBoardResponse = await viewerClient.PostAsJsonAsync($"/api/projects/{project.Id}/boards", new
        {
            name = "Forbidden Board"
        });
        Assert.Equal(HttpStatusCode.Forbidden, createBoardResponse.StatusCode);

        var createColumnResponse = await viewerClient.PostAsJsonAsync($"/api/boards/{board.Id}/columns", new
        {
            name = "Forbidden Column"
        });
        Assert.Equal(HttpStatusCode.Forbidden, createColumnResponse.StatusCode);
    }

    [Fact]
    public async Task ReorderColumns_WithCollision_RenumbersPositions()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("collision-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Collision Project");
        var board = await CreateBoardAsync(client, project.Id, "Collision Board");
        var first = await CreateColumnAsync(client, board.Id, "First");
        var second = await CreateColumnAsync(client, board.Id, "Second");
        var third = await CreateColumnAsync(client, board.Id, "Third");

        await _factory.WithDbContextAsync(async db =>
        {
            var columns = await db.Columns
                .Where(x => x.Id == first.Id || x.Id == second.Id || x.Id == third.Id)
                .ToListAsync();

            columns.Single(x => x.Id == first.Id).Position = 1000;
            columns.Single(x => x.Id == second.Id).Position = 1001;
            columns.Single(x => x.Id == third.Id).Position = 3000;

            await db.SaveChangesAsync();
        });

        var reorderResponse = await client.PutAsJsonAsync($"/api/columns/{third.Id}/reorder", new
        {
            beforeColumnId = first.Id,
            afterColumnId = second.Id
        });

        Assert.Equal(HttpStatusCode.OK, reorderResponse.StatusCode);

        var listedColumns = await client.GetFromJsonAsync<List<Column>>($"/api/boards/{board.Id}/columns");
        Assert.NotNull(listedColumns);
        Assert.Equal(new[] { first.Id, third.Id, second.Id }, listedColumns!.Select(x => x.Id));
        Assert.Equal(new[] { 1000, 2000, 3000 }, listedColumns.Select(x => x.Position));
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());
        return client;
    }

    private async Task<Project> CreateProjectAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new { name });
        response.EnsureSuccessStatusCode();

        var project = await response.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);
        return project!;
    }

    private async Task<Board> CreateBoardAsync(HttpClient client, Guid projectId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/boards", new { name });
        response.EnsureSuccessStatusCode();

        var board = await response.Content.ReadFromJsonAsync<Board>();
        Assert.NotNull(board);
        return board!;
    }

    private async Task<Column> CreateColumnAsync(HttpClient client, Guid boardId, string name)
    {
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/columns", new { name });
        response.EnsureSuccessStatusCode();

        var column = await response.Content.ReadFromJsonAsync<Column>();
        Assert.NotNull(column);
        return column!;
    }

    private async Task<Guid> SeedCardAsync(Guid columnId, string title)
    {
        var cardId = Guid.NewGuid();

        await _factory.WithDbContextAsync(async db =>
        {
            db.Cards.Add(new Card
            {
                Id = cardId,
                ColumnId = columnId,
                Title = title,
                Position = 1000,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        });

        return cardId;
    }

    private static string UniqueEmail(string prefix)
    {
        return $"{prefix}.{Guid.NewGuid():N}@example.com";
    }
}