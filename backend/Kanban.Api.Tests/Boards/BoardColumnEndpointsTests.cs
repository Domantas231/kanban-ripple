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

    [Fact]
    public async Task CreateBoard_WithEmptyName_ReturnsBadRequest()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("board-create-empty"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Create Empty Project");

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/boards", new { name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBoard_WithDuplicateName_ReturnsConflict()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("board-create-dup"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Duplicate Project");
        await CreateBoardAsync(client, project.Id, "Existing Board");

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/boards", new { name = "Existing Board" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateBoard_NonMember_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("board-create-owner"));
        var outsiderUserId = await _factory.CreateUserAsync(UniqueEmail("board-create-outsider"));

        using var ownerClient = CreateClient(ownerUserId);
        using var outsiderClient = CreateClient(outsiderUserId);

        var project = await CreateProjectAsync(ownerClient, "Forbidden Project");

        var response = await outsiderClient.PostAsJsonAsync($"/api/projects/{project.Id}/boards", new { name = "Sneaky" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoard_RenamesAndPersistsChange()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("board-update-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Update Project");
        var board = await CreateBoardAsync(client, project.Id, "Original Name");

        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", new { name = "Renamed Board", position = board.Position });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Board>();
        Assert.NotNull(updated);
        Assert.Equal("Renamed Board", updated!.Name);
    }

    [Fact]
    public async Task UpdateBoard_NotFound_ReturnsNotFound()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("board-update-missing"));
        using var client = CreateClient(ownerUserId);

        var response = await client.PutAsJsonAsync($"/api/boards/{Guid.NewGuid()}", new { name = "X", position = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoard_EmptyName_ReturnsBadRequest()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("board-update-empty"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Update Empty Project");
        var board = await CreateBoardAsync(client, project.Id, "Has Name");

        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", new { name = "   ", position = board.Position });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoard_DuplicateName_ReturnsConflict()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("board-update-dup"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Update Dup Project");
        var first = await CreateBoardAsync(client, project.Id, "First Board");
        var second = await CreateBoardAsync(client, project.Id, "Second Board");

        var response = await client.PutAsJsonAsync($"/api/boards/{second.Id}", new { name = "First Board", position = second.Position });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        _ = first; // referenced for clarity
    }

    [Fact]
    public async Task GetBoardById_NotFound_ReturnsNotFound()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("board-get-missing"));
        using var client = CreateClient(userId);

        var response = await client.GetAsync($"/api/boards/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListBoards_NonMember_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("board-list-owner"));
        var outsiderUserId = await _factory.CreateUserAsync(UniqueEmail("board-list-outsider"));

        using var ownerClient = CreateClient(ownerUserId);
        using var outsiderClient = CreateClient(outsiderUserId);

        var project = await CreateProjectAsync(ownerClient, "List Forbidden Project");

        var response = await outsiderClient.GetAsync($"/api/projects/{project.Id}/boards");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListArchivedBoards_ReturnsOnlyArchivedForUserProjects()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("board-list-archived"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Archived Boards Project");
        var activeBoard = await CreateBoardAsync(client, project.Id, "Active Board");
        var archivedBoard = await CreateBoardAsync(client, project.Id, "Archived Board");

        var archive = await client.DeleteAsync($"/api/boards/{archivedBoard.Id}");
        archive.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/boards/archived");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var archived = await response.Content.ReadFromJsonAsync<List<Board>>();
        Assert.NotNull(archived);
        Assert.Contains(archived!, b => b.Id == archivedBoard.Id);
        Assert.DoesNotContain(archived, b => b.Id == activeBoard.Id);
    }

    [Fact]
    public async Task ImportFromTrello_HappyPath_CreatesBoardColumnsCardsAndTags()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("trello-happy-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Trello Import Project");

        var trello = new
        {
            name = "Imported Board",
            lists = new[]
            {
                new { id = "list-1", name = "  Todo  ", closed = false, pos = 1.0 },
                new { id = "list-2", name = "Doing", closed = false, pos = 2.0 },
                new { id = "list-closed", name = "Old", closed = true, pos = 3.0 }
            },
            labels = new[]
            {
                new { id = "label-bug", name = "Bug", color = "red" },
                new { id = "label-noname", name = "  ", color = "blue" },
                new { id = "label-unknown-color", name = "Custom", color = "fuchsia-magic" }
            },
            cards = new[]
            {
                new
                {
                    id = "card-1", name = "  First Card  ", desc = "Card 1 desc",
                    idList = "list-1", closed = false, pos = 1.0,
                    idLabels = new[] { "label-bug", "label-unknown-color", "label-noname" }
                },
                new
                {
                    id = "card-2", name = "Second Card", desc = "",
                    idList = "list-2", closed = false, pos = 1.0,
                    idLabels = new string[0]
                },
                new
                {
                    id = "card-archived", name = "Closed Card", desc = "should be skipped",
                    idList = "list-1", closed = true, pos = 99.0,
                    idLabels = new string[0]
                },
                new
                {
                    id = "card-orphan", name = "Orphan Card", desc = "list closed",
                    idList = "list-closed", closed = false, pos = 0.5,
                    idLabels = new string[0]
                }
            }
        };

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/boards/import-trello", trello);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<Board>();
        Assert.NotNull(board);
        Assert.Equal("Imported Board", board!.Name);

        await _factory.WithDbContextAsync(async db =>
        {
            var columns = await db.Columns.Where(c => c.BoardId == board.Id).OrderBy(c => c.Position).ToListAsync();
            Assert.Equal(2, columns.Count);
            Assert.Equal("Todo", columns[0].Name);
            Assert.Equal("Doing", columns[1].Name);

            var tags = await db.Tags.Where(t => t.BoardId == board.Id).ToListAsync();
            Assert.Contains(tags, t => t.Name == "Bug" && t.Color == "#eb5a46");
            Assert.Contains(tags, t => t.Name == "Custom" && t.Color == "#808080");
            Assert.DoesNotContain(tags, t => t.Name.Trim().Length == 0);

            var cards = await db.Cards.Where(c => columns.Select(x => x.Id).Contains(c.ColumnId)).ToListAsync();
            Assert.Equal(2, cards.Count);
            Assert.Contains(cards, c => c.Title == "First Card" && c.Description == "Card 1 desc");
            Assert.Contains(cards, c => c.Title == "Second Card" && c.Description == null);

            var firstCardId = cards.Single(c => c.Title == "First Card").Id;
            var cardTags = await db.Set<CardTag>().Where(ct => ct.CardId == firstCardId).ToListAsync();
            Assert.Equal(2, cardTags.Count);
        });
    }

    [Fact]
    public async Task ImportFromTrello_WithoutBoardName_ReturnsBadRequest()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("trello-noname-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Trello No Name Project");

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/boards/import-trello", new
        {
            name = "  ",
            lists = Array.Empty<object>(),
            labels = Array.Empty<object>(),
            cards = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportFromTrello_DuplicateName_ReturnsConflict()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("trello-dup-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Trello Dup Project");
        await CreateBoardAsync(client, project.Id, "Imported");

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/boards/import-trello", new
        {
            name = "Imported",
            lists = Array.Empty<object>(),
            labels = Array.Empty<object>(),
            cards = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ImportFromTrello_NonMember_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("trello-forbid-owner"));
        var outsiderUserId = await _factory.CreateUserAsync(UniqueEmail("trello-forbid-outsider"));

        using var ownerClient = CreateClient(ownerUserId);
        using var outsiderClient = CreateClient(outsiderUserId);

        var project = await CreateProjectAsync(ownerClient, "Trello Forbid Project");

        var response = await outsiderClient.PostAsJsonAsync($"/api/projects/{project.Id}/boards/import-trello", new
        {
            name = "Should Not Happen",
            lists = Array.Empty<object>(),
            labels = Array.Empty<object>(),
            cards = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ImportFromTrello_EmptyExport_CreatesBoardWithNoColumnsOrCards()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("trello-empty-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Trello Empty Project");

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/boards/import-trello", new
        {
            name = "Skeleton Board",
            lists = Array.Empty<object>(),
            labels = Array.Empty<object>(),
            cards = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<Board>();
        Assert.NotNull(board);

        await _factory.WithDbContextAsync(async db =>
        {
            Assert.False(await db.Columns.AnyAsync(c => c.BoardId == board!.Id));
            Assert.False(await db.Cards.AnyAsync(c => db.Columns.Any(col => col.BoardId == board!.Id && col.Id == c.ColumnId)));
            Assert.False(await db.Tags.AnyAsync(t => t.BoardId == board!.Id));
        });
    }

    [Fact]
    public async Task ImportFromTrello_DuplicateLabelNames_ReusesSingleTag()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("trello-dup-label"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Trello Dup Label Project");

        var trello = new
        {
            name = "Dup Label Board",
            lists = new[] { new { id = "L1", name = "Todo", closed = false, pos = 1.0 } },
            labels = new[]
            {
                new { id = "lbl-1", name = "Priority", color = "red" },
                new { id = "lbl-2", name = "priority", color = "blue" }
            },
            cards = Array.Empty<object>()
        };

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/boards/import-trello", trello);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<Board>();
        Assert.NotNull(board);

        await _factory.WithDbContextAsync(async db =>
        {
            var tags = await db.Tags.Where(t => t.BoardId == board!.Id).ToListAsync();
            Assert.Single(tags);
        });
    }

    [Fact]
    public async Task ArchiveColumn_CascadesToCardsAndRemovesSubscriptions()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("col-archive-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Column Archive Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Archivable");

        var cardA = await SeedCardAsync(column.Id, "Card A");
        var cardB = await SeedCardAsync(column.Id, "Card B");

        var columnSub = await client.PostAsJsonAsync("/api/subscriptions", new { entityType = EntityType.Column, entityId = column.Id });
        columnSub.EnsureSuccessStatusCode();
        var cardSub = await client.PostAsJsonAsync("/api/subscriptions", new { entityType = EntityType.Card, entityId = cardA });
        cardSub.EnsureSuccessStatusCode();

        var archive = await client.DeleteAsync($"/api/columns/{column.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var archivedColumn = await db.Columns.IgnoreQueryFilters().SingleAsync(x => x.Id == column.Id);
            Assert.NotNull(archivedColumn.DeletedAt);

            var cards = await db.Cards.IgnoreQueryFilters().Where(x => x.ColumnId == column.Id).ToListAsync();
            Assert.All(cards, c => Assert.NotNull(c.DeletedAt));

            var subs = await db.Subscriptions
                .Where(s =>
                    (s.EntityType == EntityType.Column && s.EntityId == column.Id) ||
                    (s.EntityType == EntityType.Card && (s.EntityId == cardA || s.EntityId == cardB)))
                .ToListAsync();
            Assert.Empty(subs);
        });
    }

    [Fact]
    public async Task ArchiveColumn_NotFound_ReturnsNotFound()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("col-arch-missing"));
        using var client = CreateClient(userId);

        var response = await client.DeleteAsync($"/api/columns/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveColumn_NonMember_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("col-arch-owner"));
        var outsiderUserId = await _factory.CreateUserAsync(UniqueEmail("col-arch-outsider"));

        using var ownerClient = CreateClient(ownerUserId);
        using var outsiderClient = CreateClient(outsiderUserId);

        var project = await CreateProjectAsync(ownerClient, "Forbidden Archive Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Column");

        var response = await outsiderClient.DeleteAsync($"/api/columns/{column.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RestoreColumn_RestoresColumnAndCards()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("col-restore-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Column Restore Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Restorable");
        var cardId = await SeedCardAsync(column.Id, "Card");

        var archive = await client.DeleteAsync($"/api/columns/{column.Id}");
        archive.EnsureSuccessStatusCode();

        var restore = await client.PostAsync($"/api/columns/{column.Id}/restore", null);
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var restored = await db.Columns.SingleAsync(x => x.Id == column.Id);
            Assert.Null(restored.DeletedAt);

            var card = await db.Cards.SingleAsync(x => x.Id == cardId);
            Assert.Null(card.DeletedAt);
        });
    }

    [Fact]
    public async Task RestoreColumn_NotFound_ReturnsNotFound()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("col-restore-missing"));
        using var client = CreateClient(userId);

        var response = await client.PostAsync($"/api/columns/{Guid.NewGuid()}/restore", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RestoreColumn_NonMember_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("col-rest-owner"));
        var outsiderUserId = await _factory.CreateUserAsync(UniqueEmail("col-rest-outsider"));

        using var ownerClient = CreateClient(ownerUserId);
        using var outsiderClient = CreateClient(outsiderUserId);

        var project = await CreateProjectAsync(ownerClient, "Forbidden Restore Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Column");

        var archive = await ownerClient.DeleteAsync($"/api/columns/{column.Id}");
        archive.EnsureSuccessStatusCode();

        var response = await outsiderClient.PostAsync($"/api/columns/{column.Id}/restore", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListArchivedColumns_ByBoard_ReturnsArchivedOnly()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("col-list-arch"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Archived Cols Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var active = await CreateColumnAsync(client, board.Id, "Active");
        var archived = await CreateColumnAsync(client, board.Id, "Archived");

        var archive = await client.DeleteAsync($"/api/columns/{archived.Id}");
        archive.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/boards/{board.Id}/columns/archived");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var archivedColumns = await response.Content.ReadFromJsonAsync<List<Column>>();
        Assert.NotNull(archivedColumns);
        Assert.Contains(archivedColumns!, c => c.Id == archived.Id);
        Assert.DoesNotContain(archivedColumns, c => c.Id == active.Id);
    }

    [Fact]
    public async Task ListArchivedColumns_ByBoard_AsViewer_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("col-arch-viewer-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Archived Cols Viewer Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Archived");
        var archive = await ownerClient.DeleteAsync($"/api/columns/{column.Id}");
        archive.EnsureSuccessStatusCode();

        var viewerUserId = await _factory.CreateUserAsync(UniqueEmail("col-arch-viewer"));
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

        var response = await viewerClient.GetAsync($"/api/boards/{board.Id}/columns/archived");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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