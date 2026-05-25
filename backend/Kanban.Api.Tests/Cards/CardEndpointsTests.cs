using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Services.Cards;
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
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null,
            version = card.Version
        });
        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);

        var staleVersionUpdate = await client.PutAsJsonAsync($"/api/cards/{card.Id}", new
        {
            title = "Versioned Card - Stale",
            description = "stale",
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null,
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
    public async Task RestoreCard_WhenColumnIsArchived_ReturnsBadRequest()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-restore-archived-column-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Card Restore Archived Column Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Restorable Card");

        var archiveCardResponse = await client.DeleteAsync($"/api/cards/{card.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveCardResponse.StatusCode);

        var archiveColumnResponse = await client.DeleteAsync($"/api/columns/{column.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveColumnResponse.StatusCode);

        var restoreResponse = await client.PostAsync($"/api/cards/{card.Id}/restore", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, restoreResponse.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var archivedCard = await db.Cards
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == card.Id);

            Assert.NotNull(archivedCard.DeletedAt);
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
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null
        });
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        var updateResponse = await viewerClient.PutAsJsonAsync($"/api/cards/{card.Id}", new
        {
            title = "Should Fail",
            description = "viewer cannot edit",
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null,
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

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("card-getbyid-missing"));
        using var client = CreateClient(userId);

        var response = await client.GetAsync($"/api/cards/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNotFound()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("card-update-missing"));
        using var client = CreateClient(userId);

        var response = await client.PutAsJsonAsync($"/api/cards/{Guid.NewGuid()}", new
        {
            title = "Updated",
            description = "missing",
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null,
            version = 1
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignAndUnassignTag_RoundTrip_Works()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-tag-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Tag Round Trip Project");
        var board = await CreateBoardAsync(client, project.Id, "Tag Round Trip Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Tagged Card");

        var tagResponse = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tags", new
        {
            name = "Bug",
            color = "#FF0000"
        });
        tagResponse.EnsureSuccessStatusCode();
        var tag = await tagResponse.Content.ReadFromJsonAsync<Tag>();
        Assert.NotNull(tag);

        var assignResponse = await client.PostAsync($"/api/cards/{card.Id}/tags/{tag!.Id}", null);
        Assert.Equal(HttpStatusCode.NoContent, assignResponse.StatusCode);

        var afterAssign = await client.GetAsync($"/api/cards/{card.Id}");
        var assignedCard = await afterAssign.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(assignedCard);
        Assert.Contains(assignedCard!.CardTags, ct => ct.TagId == tag.Id);

        var unassignResponse = await client.DeleteAsync($"/api/cards/{card.Id}/tags/{tag.Id}");
        Assert.Equal(HttpStatusCode.NoContent, unassignResponse.StatusCode);

        var afterUnassign = await client.GetAsync($"/api/cards/{card.Id}");
        var unassignedCard = await afterUnassign.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(unassignedCard);
        Assert.DoesNotContain(unassignedCard!.CardTags, ct => ct.TagId == tag.Id);
    }

    [Fact]
    public async Task AssignAndUnassignUser_RoundTrip_Works()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-assign-owner"));
        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("card-assign-member"));

        using var client = CreateClient(ownerUserId);
        var project = await CreateProjectAsync(client, "Assignee Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Assignee Card");

        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = memberUserId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        var assignResponse = await client.PostAsync($"/api/cards/{card.Id}/assignees/{memberUserId}", null);
        Assert.Equal(HttpStatusCode.NoContent, assignResponse.StatusCode);

        var afterAssign = await client.GetAsync($"/api/cards/{card.Id}");
        var assignedCard = await afterAssign.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(assignedCard);
        Assert.Contains(assignedCard!.Assignments, a => a.UserId == memberUserId);

        var unassignResponse = await client.DeleteAsync($"/api/cards/{card.Id}/assignees/{memberUserId}");
        Assert.Equal(HttpStatusCode.NoContent, unassignResponse.StatusCode);

        var afterUnassign = await client.GetAsync($"/api/cards/{card.Id}");
        var unassignedCard = await afterUnassign.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(unassignedCard);
        Assert.DoesNotContain(unassignedCard!.Assignments, a => a.UserId == memberUserId);
    }

    [Fact]
    public async Task Schedule_UpdatesStartAndDueDates()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-schedule-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Schedule Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Scheduled Card");

        var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc);

        var response = await client.PutAsJsonAsync($"/api/cards/{card.Id}/schedule", new
        {
            startDate = start,
            dueDate = due
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(updated);
        Assert.Equal(start, updated!.StartDate);
        Assert.Equal(due, updated.DueDate);
    }

    [Fact]
    public async Task ListCardActivities_AfterMutations_ReturnsActivities()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-activities-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Activities Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Activity Card");

        var update = await client.PutAsJsonAsync($"/api/cards/{card.Id}", new
        {
            title = "Activity Card Updated",
            description = "now updated",
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null,
            version = card.Version
        });
        update.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/cards/{card.Id}/activities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var activities = await response.Content.ReadFromJsonAsync<List<CardActivity>>();
        Assert.NotNull(activities);
        Assert.NotEmpty(activities!);
    }

    [Fact]
    public async Task ListCardActivities_NotFoundCard_Returns404()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("card-act-missing"));
        using var client = CreateClient(userId);

        var response = await client.GetAsync($"/api/cards/{Guid.NewGuid()}/activities");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListProjectActivities_AfterMutations_ReturnsActivities()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-activities-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Project Activity Test");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Project Activity Card");

        var update = await client.PutAsJsonAsync($"/api/cards/{card.Id}", new
        {
            title = "Project Activity Card v2",
            description = "v2",
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null,
            version = card.Version
        });
        update.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"/api/projects/{project.Id}/activities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var activities = await response.Content.ReadFromJsonAsync<List<ProjectActivityDto>>();
        Assert.NotNull(activities);
        Assert.NotEmpty(activities!);
    }

    [Fact]
    public async Task ListProjectActivities_OutsiderForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-act-owner"));
        var outsiderUserId = await _factory.CreateUserAsync(UniqueEmail("proj-act-outsider"));

        using var ownerClient = CreateClient(ownerUserId);
        using var outsiderClient = CreateClient(outsiderUserId);

        var project = await CreateProjectAsync(ownerClient, "Outsider Activity Project");

        var response = await outsiderClient.GetAsync($"/api/projects/{project.Id}/activities");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListArchived_ReturnsOnlyArchivedCardsForUserProjects()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-list-archived"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Archived Cards Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var activeCard = await CreateCardAsync(client, column.Id, "Active Card");
        var archivedCard = await CreateCardAsync(client, column.Id, "Archived Card");

        var archive = await client.DeleteAsync($"/api/cards/{archivedCard.Id}");
        archive.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/cards/archived");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<Card>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, c => c.Id == archivedCard.Id);
        Assert.DoesNotContain(page.Items, c => c.Id == activeCard.Id);
    }

    [Fact]
    public async Task ListArchived_AsViewer_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-arch-viewer-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Archived Cards Viewer Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");
        var card = await CreateCardAsync(ownerClient, column.Id, "Archived Card");

        var archive = await ownerClient.DeleteAsync($"/api/cards/{card.Id}");
        archive.EnsureSuccessStatusCode();

        var viewerUserId = await _factory.CreateUserAsync(UniqueEmail("card-arch-viewer"));
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

        var listResponse = await viewerClient.GetAsync("/api/cards/archived");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PaginatedResponse<Card>>();
        Assert.NotNull(page);
        Assert.DoesNotContain(page!.Items, c => c.Id == card.Id);

        var byBoardResponse = await viewerClient.GetAsync($"/api/boards/{board.Id}/cards/archived");
        Assert.Equal(HttpStatusCode.Forbidden, byBoardResponse.StatusCode);
    }

    [Fact]
    public async Task ListArchivedByBoard_ReturnsArchivedForGivenBoardOnly()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("card-list-arch-by-board"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Board-Scoped Archive Project");
        var boardA = await CreateBoardAsync(client, project.Id, "Board A");
        var boardB = await CreateBoardAsync(client, project.Id, "Board B");
        var columnA = await CreateColumnAsync(client, boardA.Id, "Todo A");
        var columnB = await CreateColumnAsync(client, boardB.Id, "Todo B");

        var cardA = await CreateCardAsync(client, columnA.Id, "Card A");
        var cardB = await CreateCardAsync(client, columnB.Id, "Card B");

        await client.DeleteAsync($"/api/cards/{cardA.Id}");
        await client.DeleteAsync($"/api/cards/{cardB.Id}");

        var response = await client.GetAsync($"/api/boards/{boardA.Id}/cards/archived");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var archivedInA = await response.Content.ReadFromJsonAsync<PaginatedResponse<Card>>();
        Assert.NotNull(archivedInA);
        Assert.Contains(archivedInA!.Items, c => c.Id == cardA.Id);
        Assert.DoesNotContain(archivedInA.Items, c => c.Id == cardB.Id);
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
            startDate = (DateTime?)null,
            dueDate = (DateTime?)null
        });
        response.EnsureSuccessStatusCode();

        var card = await response.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(card);
        return card!;
    }
}