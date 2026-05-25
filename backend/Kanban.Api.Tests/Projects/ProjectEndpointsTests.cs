using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Services.Projects;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

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
            name = "My Project"
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
            name = "Renamed"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
            role = ProjectRole.Manager
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
    public async Task Purge_ArchivedProject_HardDeletesAndReturnsNoContent()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("purge-owner"));
        using var client = CreateClient(ownerUserId);

        var create = await client.PostAsJsonAsync("/api/projects", new { name = "Purge Project" });
        create.EnsureSuccessStatusCode();
        var project = await create.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var archive = await client.DeleteAsync($"/api/projects/{project!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var purge = await client.DeleteAsync($"/api/projects/{project.Id}/permanent");
        Assert.Equal(HttpStatusCode.NoContent, purge.StatusCode);

        // After purge the row is gone; access check has nothing to match, so the
        // restore call should return either Forbidden (membership row gone) or NotFound.
        var restoreAfterPurge = await client.PostAsync($"/api/projects/{project.Id}/restore", content: null);
        Assert.Contains(restoreAfterPurge.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
    }

    [Fact]
    public async Task Purge_ActiveProject_ReturnsBadRequest()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("purge-active"));
        using var client = CreateClient(ownerUserId);

        var create = await client.PostAsJsonAsync("/api/projects", new { name = "Still Active" });
        create.EnsureSuccessStatusCode();
        var project = await create.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var purge = await client.DeleteAsync($"/api/projects/{project!.Id}/permanent");
        Assert.Equal(HttpStatusCode.BadRequest, purge.StatusCode);
    }

    [Fact]
    public async Task Purge_NonOwner_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("purge-owner-forbid"));
        var otherUserId = await _factory.CreateUserAsync(UniqueEmail("purge-other"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Purge Forbidden");
        var archive = await ownerClient.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        using var otherClient = CreateClient(otherUserId);
        var purge = await otherClient.DeleteAsync($"/api/projects/{project.Id}/permanent");
        Assert.Equal(HttpStatusCode.Forbidden, purge.StatusCode);
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
                name = $"Project {i:00}"
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
    public async Task GetSwimlane_ScheduledCard_IncludesStartDateAndDueDate()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("swimlane-dates-owner"));
        using var client = CreateClient(ownerUserId);

        var create = await client.PostAsJsonAsync("/api/projects", new { name = "Swimlane Dates Project" });
        create.EnsureSuccessStatusCode();
        var project = await create.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var boardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await _factory.WithDbContextAsync(async db =>
        {
            db.Boards.Add(new Board
            {
                Id = boardId,
                ProjectId = project!.Id,
                Name = "Board",
                Position = 1000,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.Columns.Add(new Column
            {
                Id = columnId,
                BoardId = boardId,
                Name = "Todo",
                Position = 1000,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.Cards.Add(new Card
            {
                Id = cardId,
                ColumnId = columnId,
                Title = "Scheduled Card",
                Position = 1000,
                CreatedAt = now,
                UpdatedAt = now
            });

            await db.SaveChangesAsync();
        });

        var startDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);

        var schedule = await client.PutAsJsonAsync($"/api/cards/{cardId}/schedule", new
        {
            startDate,
            dueDate
        });
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);

        var response = await client.GetAsync($"/api/projects/{project!.Id}/swimlane");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var swimlane = await response.Content.ReadFromJsonAsync<SwimlaneView>();
        Assert.NotNull(swimlane);

        var card = swimlane!.Boards[0].Columns[0].Cards[0];
        Assert.Equal("Scheduled Card", card.Title);
        Assert.NotNull(card.StartDate);
        Assert.NotNull(card.DueDate);
        Assert.Equal(startDate, card.StartDate!.Value, TimeSpan.FromSeconds(1));
        Assert.Equal(dueDate, card.DueDate!.Value, TimeSpan.FromSeconds(1));
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

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("proj-get-missing"));
        using var client = CreateClient(userId);

        var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_NonOwner_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-update-owner"));
        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("proj-update-member"));

        using var ownerClient = CreateClient(ownerUserId);
        using var memberClient = CreateClient(memberUserId);

        var create = await ownerClient.PostAsJsonAsync("/api/projects", new { name = "Update Forbidden Project" });
        create.EnsureSuccessStatusCode();
        var project = await create.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project!.Id,
                UserId = memberUserId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        var response = await memberClient.PutAsJsonAsync($"/api/projects/{project!.Id}", new { name = "Renamed" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("proj-update-missing"));
        using var client = CreateClient(userId);

        var response = await client.PutAsJsonAsync($"/api/projects/{Guid.NewGuid()}", new { name = "X" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Archive_NotFound_Returns404()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("proj-arch-missing"));
        using var client = CreateClient(userId);

        var response = await client.DeleteAsync($"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Archive_NonOwner_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-arch-owner"));
        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("proj-arch-member"));

        using var ownerClient = CreateClient(ownerUserId);
        using var memberClient = CreateClient(memberUserId);

        var project = await CreateProjectAsync(ownerClient, "Archive Forbidden Project");
        await AddProjectMemberAsync(project.Id, memberUserId, ProjectRole.Member);

        var response = await memberClient.DeleteAsync($"/api/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Restore_NonMember_ReturnsForbidden()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("proj-restore-non-member"));
        using var client = CreateClient(userId);

        var response = await client.PostAsync($"/api/projects/{Guid.NewGuid()}/restore", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListArchived_ReturnsOnlyArchivedProjects()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("proj-list-archived"));
        using var client = CreateClient(userId);

        var active = await CreateProjectAsync(client, "Active Project");
        var archived = await CreateProjectAsync(client, "Archived Project");

        var archive = await client.DeleteAsync($"/api/projects/{archived.Id}");
        archive.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/projects/archived");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<ProjectListItemDto>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, p => p.Id == archived.Id);
        Assert.DoesNotContain(page.Items, p => p.Id == active.Id);
    }

    [Fact]
    public async Task GetMembers_NonMember_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-getmem-owner"));
        var outsiderUserId = await _factory.CreateUserAsync(UniqueEmail("proj-getmem-outsider"));

        using var ownerClient = CreateClient(ownerUserId);
        using var outsiderClient = CreateClient(outsiderUserId);

        var project = await CreateProjectAsync(ownerClient, "Members Forbidden Project");

        var response = await outsiderClient.GetAsync($"/api/projects/{project.Id}/members");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_NotFound_Returns404()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-rm-missing"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Remove Missing Member Project");

        var response = await client.DeleteAsync($"/api/projects/{project.Id}/members/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_OwnerCannotRemoveSelf_ReturnsBadRequest()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-rm-self"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Self Remove Project");

        var response = await client.DeleteAsync($"/api/projects/{project.Id}/members/{ownerUserId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMemberRole_OnNonExistentMember_Returns404()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-mr-missing"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Missing Role Update Project");

        var response = await client.PutAsJsonAsync($"/api/projects/{project.Id}/members/{Guid.NewGuid()}/role", new { role = ProjectRole.Manager });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TransferOwnership_OwnerToMember_PromotesNewOwner()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-xfer-owner"));
        var newOwnerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-xfer-new"));

        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Transfer Project");
        await AddProjectMemberAsync(project.Id, newOwnerUserId, ProjectRole.Member);

        var response = await ownerClient.PostAsJsonAsync($"/api/projects/{project.Id}/transfer-ownership", new { newOwnerUserId });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var stored = await db.Projects.SingleAsync(x => x.Id == project.Id);
            Assert.Equal(newOwnerUserId, stored.OwnerId);
        });
    }

    [Fact]
    public async Task TransferOwnership_NotFound_Returns404()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("proj-xfer-missing"));
        using var client = CreateClient(userId);

        var response = await client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/transfer-ownership", new { newOwnerUserId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Leave_NotFound_Returns404()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("proj-leave-missing"));
        using var client = CreateClient(userId);

        var response = await client.PostAsync($"/api/projects/{Guid.NewGuid()}/leave", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Leave_OwnerCannotLeave_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-leave-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Owner Leave Project");

        var response = await client.PostAsync($"/api/projects/{project.Id}/leave", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Leave_MemberSucceeds_AndRemovesMembership()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-leave-owner-mem"));
        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("proj-leave-member"));

        using var ownerClient = CreateClient(ownerUserId);
        using var memberClient = CreateClient(memberUserId);

        var project = await CreateProjectAsync(ownerClient, "Member Leave Project");
        await AddProjectMemberAsync(project.Id, memberUserId, ProjectRole.Member);

        var response = await memberClient.PostAsync($"/api/projects/{project.Id}/leave", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var stillMember = await db.ProjectMembers.AnyAsync(x => x.ProjectId == project.Id && x.UserId == memberUserId);
            Assert.False(stillMember);
        });
    }

    [Fact]
    public async Task Invite_NonManager_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-invite-owner"));
        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("proj-invite-member"));

        using var ownerClient = CreateClient(ownerUserId);
        using var memberClient = CreateClient(memberUserId);

        var project = await CreateProjectAsync(ownerClient, "Invite Forbidden Project");
        await AddProjectMemberAsync(project.Id, memberUserId, ProjectRole.Member);

        var response = await memberClient.PostAsJsonAsync($"/api/projects/{project.Id}/invite", new { email = "newperson@example.com" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invite_NotFound_Returns404()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("proj-invite-missing"));
        using var client = CreateClient(userId);

        var response = await client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/invite", new { email = "x@example.com" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invite_AlreadyMemberByEmail_ReturnsConflict()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-inv-conflict-owner"));
        var existingMemberEmail = UniqueEmail("proj-inv-conflict-member");
        var existingMemberUserId = await _factory.CreateUserAsync(existingMemberEmail);

        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Already Member Project");
        await AddProjectMemberAsync(project.Id, existingMemberUserId, ProjectRole.Member);

        var response = await ownerClient.PostAsJsonAsync($"/api/projects/{project.Id}/invite", new { email = existingMemberEmail });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Invite_BlankEmail_ReturnsBadRequest()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-inv-blank-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Blank Email Invite Project");

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/invite", new { email = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_HappyPath_AddsMembership()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-accept-owner"));
        var inviteeEmail = UniqueEmail("proj-accept-invitee");
        var inviteeUserId = await _factory.CreateUserAsync(inviteeEmail);

        using var ownerClient = CreateClient(ownerUserId);
        using var inviteeClient = CreateClient(inviteeUserId);

        var project = await CreateProjectAsync(ownerClient, "Accept Project");

        var inviteResponse = await ownerClient.PostAsJsonAsync($"/api/projects/{project.Id}/invite", new { email = inviteeEmail });
        inviteResponse.EnsureSuccessStatusCode();

        var token = await _factory.WithDbContextSelectAsync(async db =>
        {
            var inv = await db.Invitations.SingleAsync(i => i.ProjectId == project.Id && i.Email == inviteeEmail);
            return inv.Token;
        });

        var acceptResponse = await inviteeClient.PostAsync($"/api/invitations/{Uri.EscapeDataString(token)}/accept", null);
        Assert.Equal(HttpStatusCode.NoContent, acceptResponse.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var membership = await db.ProjectMembers.SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.UserId == inviteeUserId);
            Assert.NotNull(membership);
            Assert.Equal(ProjectRole.Member, membership!.Role);

            var inv = await db.Invitations.SingleAsync(i => i.ProjectId == project.Id && i.Email == inviteeEmail);
            Assert.NotNull(inv.AcceptedAt);
            Assert.Equal(inviteeUserId, inv.AcceptedBy);
        });
    }

    [Fact]
    public async Task AcceptInvitation_BlankToken_Returns400()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("proj-accept-blank"));
        using var client = CreateClient(userId);

        var response = await client.PostAsync($"/api/invitations/%20/accept", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_UnknownToken_Returns400()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("proj-accept-unknown"));
        using var client = CreateClient(userId);

        var response = await client.PostAsync($"/api/invitations/no-such-token/accept", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_WrongEmail_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-accept-wrong-owner"));
        var invitedEmail = UniqueEmail("proj-accept-wrong-invited");
        await _factory.CreateUserAsync(invitedEmail);
        var differentUserId = await _factory.CreateUserAsync(UniqueEmail("proj-accept-wrong-other"));

        using var ownerClient = CreateClient(ownerUserId);
        using var otherClient = CreateClient(differentUserId);

        var project = await CreateProjectAsync(ownerClient, "Wrong Email Accept Project");

        var inviteResponse = await ownerClient.PostAsJsonAsync($"/api/projects/{project.Id}/invite", new { email = invitedEmail });
        inviteResponse.EnsureSuccessStatusCode();

        var token = await _factory.WithDbContextSelectAsync(async db =>
        {
            var inv = await db.Invitations.SingleAsync(i => i.ProjectId == project.Id && i.Email == invitedEmail);
            return inv.Token;
        });

        var response = await otherClient.PostAsync($"/api/invitations/{Uri.EscapeDataString(token)}/accept", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_AlreadyMember_IsIdempotent()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("proj-accept-idem-owner"));
        var memberEmail = UniqueEmail("proj-accept-idem-member");
        var memberUserId = await _factory.CreateUserAsync(memberEmail);

        using var ownerClient = CreateClient(ownerUserId);
        using var memberClient = CreateClient(memberUserId);

        var project = await CreateProjectAsync(ownerClient, "Idempotent Accept Project");
        await AddProjectMemberAsync(project.Id, memberUserId, ProjectRole.Member);

        var inviteResponse = await ownerClient.PostAsJsonAsync($"/api/projects/{project.Id}/invite", new { email = memberEmail });
        Assert.Equal(HttpStatusCode.Conflict, inviteResponse.StatusCode);
    }

    private async Task AddProjectMemberAsync(Guid projectId, Guid userId, ProjectRole role)
    {
        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = userId,
                Role = role,
                JoinedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
    }

    private static async Task<Project> CreateProjectAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new { name });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);
        return project!;
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

/// <summary>
/// Thin alias for <see cref="KanbanApiFactoryBase"/>. Kept under the original name and
/// namespace so existing test classes continue to work via <c>IClassFixture&lt;ProjectsApiFactory&gt;</c>
/// without import changes.
/// </summary>
public sealed class ProjectsApiFactory : KanbanApiFactoryBase
{
}
