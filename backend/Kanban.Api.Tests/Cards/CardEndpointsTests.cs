using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Services.Projects;
using Kanban.Api.Tests.Projects;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Cards;

public sealed class CardEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public CardEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Update_WithVersionMismatch_ReturnsConflict()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-version-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Card Version Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Versioned Card");

        var firstUpdate = await client.PutAsJsonAsync($"/api/cards/{card.Id}", new
        {
            title = "Versioned Card - Updated",
            description = "updated",
            plannedDurationHours = 2.5m,
            version = card.Version
        });
        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);

        var staleVersionUpdate = await client.PutAsJsonAsync($"/api/cards/{card.Id}", new
        {
            title = "Versioned Card - Stale",
            description = "stale",
            plannedDurationHours = 1.0m,
            version = card.Version
        });

        Assert.Equal(HttpStatusCode.Conflict, staleVersionUpdate.StatusCode);
    }

    [Fact]
    public async Task Move_WithoutVersionCheck_LastWriteWins()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-move-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Card Move Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var sourceColumn = await CreateColumnAsync(client, board.Id, "Source");
        var firstTargetColumn = await CreateColumnAsync(client, board.Id, "Target A");
        var secondTargetColumn = await CreateColumnAsync(client, board.Id, "Target B");
        var card = await CreateCardAsync(client, sourceColumn.Id, "Movable Card");

        var firstMove = await client.PutAsJsonAsync($"/api/cards/{card.Id}/move", new
        {
            columnId = firstTargetColumn.Id,
            position = 0
        });
        Assert.Equal(HttpStatusCode.OK, firstMove.StatusCode);

        var secondMove = await client.PutAsJsonAsync($"/api/cards/{card.Id}/move", new
        {
            columnId = secondTargetColumn.Id,
            position = 0
        });
        Assert.Equal(HttpStatusCode.OK, secondMove.StatusCode);

        var getMoved = await client.GetAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.OK, getMoved.StatusCode);

        var movedCard = await getMoved.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(movedCard);
        Assert.Equal(secondTargetColumn.Id, movedCard!.ColumnId);
    }

    [Fact]
    public async Task ArchiveCard_CascadesToAttachmentsAndSubtasks()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-archive-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Card Archive Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Archivable Card");

        var attachmentId = Guid.NewGuid();
        var subtaskId = Guid.NewGuid();

        await _factory.WithDbContextAsync(async db =>
        {
            db.Attachments.Add(new Attachment
            {
                Id = attachmentId,
                CardId = card.Id,
                Filename = "spec.pdf",
                FileSize = 1024,
                StorageKey = "attachments/spec.pdf",
                MimeType = "application/pdf",
                UploadedAt = DateTime.UtcNow
            });

            db.Subtasks.Add(new Subtask
            {
                Id = subtaskId,
                CardId = card.Id,
                Description = "Do the thing",
                Completed = false,
                Position = 1000,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        });

        var archiveResponse = await client.DeleteAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        var getArchived = await client.GetAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getArchived.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var archivedCard = await db.Cards
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == card.Id);
            Assert.NotNull(archivedCard.DeletedAt);

            var archivedAttachment = await db.Attachments
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == attachmentId);
            Assert.NotNull(archivedAttachment.DeletedAt);

            var archivedSubtask = await db.Subtasks
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == subtaskId);
            Assert.NotNull(archivedSubtask.DeletedAt);
        });
    }

    [Fact]
    public async Task RestoreCard_ReversesCascade()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-restore-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Card Restore Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Restorable Card");

        var attachmentId = Guid.NewGuid();
        var subtaskId = Guid.NewGuid();

        await _factory.WithDbContextAsync(async db =>
        {
            db.Attachments.Add(new Attachment
            {
                Id = attachmentId,
                CardId = card.Id,
                Filename = "notes.txt",
                FileSize = 256,
                StorageKey = "attachments/notes.txt",
                MimeType = "text/plain",
                UploadedAt = DateTime.UtcNow
            });

            db.Subtasks.Add(new Subtask
            {
                Id = subtaskId,
                CardId = card.Id,
                Description = "Restore me",
                Completed = false,
                Position = 1000,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        });

        var archiveResponse = await client.DeleteAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        var restoreResponse = await client.PostAsync($"/api/cards/{card.Id}/restore", content: null);
        Assert.Equal(HttpStatusCode.NoContent, restoreResponse.StatusCode);

        var getRestored = await client.GetAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.OK, getRestored.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var restoredCard = await db.Cards
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == card.Id);
            Assert.Null(restoredCard.DeletedAt);

            var restoredAttachment = await db.Attachments
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == attachmentId);
            Assert.Null(restoredAttachment.DeletedAt);

            var restoredSubtask = await db.Subtasks
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == subtaskId);
            Assert.Null(restoredSubtask.DeletedAt);
        });
    }

    [Fact]
    public async Task Viewer_CanRead_ButCannotCreateEditOrDelete()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-viewer-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Viewer Access Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Main Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");
        var card = await CreateCardAsync(ownerClient, column.Id, "Viewer Card");

        var viewerUserId = await _factory.CreateUserAsync(UniqueEmail("card-viewer-user"));
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

        var getCardResponse = await viewerClient.GetAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.OK, getCardResponse.StatusCode);

        var listCardsResponse = await viewerClient.GetAsync($"/api/boards/{board.Id}/cards");
        Assert.Equal(HttpStatusCode.OK, listCardsResponse.StatusCode);

        var createResponse = await viewerClient.PostAsJsonAsync($"/api/columns/{column.Id}/cards", new
        {
            title = "Should Fail",
            description = "viewer cannot create",
            plannedDurationHours = 1.0m
        });
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        var updateResponse = await viewerClient.PutAsJsonAsync($"/api/cards/{card.Id}", new
        {
            title = "Should Fail",
            description = "viewer cannot edit",
            plannedDurationHours = 2.0m,
            version = card.Version
        });
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);

        var deleteResponse = await viewerClient.DeleteAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Search_WithoutQuery_ReturnsBadRequest()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("card-search-required"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Search Required Project");

        var response = await client.GetAsync($"/api/projects/{project.Id}/cards/search");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_ViewerCanAccess_AndGetsScopedResults()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-search-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Card Search Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Search Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Search Column");
        await CreateCardAsync(ownerClient, column.Id, "Kanban Search Hit");

        var viewerUserId = await _factory.CreateUserAsync(UniqueEmail("card-search-viewer"));
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

        var response = await viewerClient.GetAsync($"/api/projects/{project.Id}/cards/search?q=kan");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PaginatedResponse<Card>>();
        Assert.NotNull(payload);
        Assert.Single(payload!.Items);
        Assert.Contains(payload.Items, x => x.Title.Contains("Kan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Filter_WithoutCriteria_ReturnsAllBoardCards()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("card-filter-all"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Filter All Project");
        var board = await CreateBoardAsync(client, project.Id, "Filter Board");
        var firstColumn = await CreateColumnAsync(client, board.Id, "Todo");
        var secondColumn = await CreateColumnAsync(client, board.Id, "Done");

        var firstCard = await CreateCardAsync(client, firstColumn.Id, "First Card");
        var secondCard = await CreateCardAsync(client, secondColumn.Id, "Second Card");

        var response = await client.GetAsync($"/api/boards/{board.Id}/cards/filter");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<Card>>();
        Assert.NotNull(payload);
        var ids = payload!.Select(x => x.Id).ToHashSet();
        Assert.Contains(firstCard.Id, ids);
        Assert.Contains(secondCard.Id, ids);
    }

    [Fact]
    public async Task Filter_ByColumnIds_ReturnsMatchingCardsOnly()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("card-filter-column"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Filter Column Project");
        var board = await CreateBoardAsync(client, project.Id, "Filter Board");
        var includedColumn = await CreateColumnAsync(client, board.Id, "Included");
        var excludedColumn = await CreateColumnAsync(client, board.Id, "Excluded");

        var includedCard = await CreateCardAsync(client, includedColumn.Id, "Included Card");
        await CreateCardAsync(client, excludedColumn.Id, "Excluded Card");

        var response = await client.GetAsync($"/api/boards/{board.Id}/cards/filter?columnIds={includedColumn.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<Card>>();
        Assert.NotNull(payload);
        var single = Assert.Single(payload!);
        Assert.Equal(includedCard.Id, single.Id);
    }

    [Fact]
    public async Task Filter_WithInvalidGuid_ReturnsBadRequest()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("card-filter-bad-guid"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Filter Invalid Guid Project");
        var board = await CreateBoardAsync(client, project.Id, "Filter Board");

        var response = await client.GetAsync($"/api/boards/{board.Id}/cards/filter?tagIds=not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
            plannedDurationHours = 1.0m
        });
        response.EnsureSuccessStatusCode();

        var card = await response.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(card);
        return card!;
    }
}