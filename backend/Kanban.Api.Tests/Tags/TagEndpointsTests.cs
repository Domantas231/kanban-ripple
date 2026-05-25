using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Tests.Projects;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Tests.Tags;

public sealed class TagEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public TagEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListByBoard_ReturnsAllTags()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-list-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Tag List Project");
        var board = await CreateBoardAsync(client, project.Id, "Tag List Board");
        var createdA = await CreateTagAsync(client, board.Id, "Bug", "#AA11CC");
        var createdB = await CreateTagAsync(client, board.Id, "Feature", "#11CCAA");

        var response = await client.GetAsync($"/api/boards/{board.Id}/tags");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tags = await response.Content.ReadFromJsonAsync<List<Tag>>();
        Assert.NotNull(tags);
        Assert.Contains(tags!, x => x.Id == createdA.Id && x.Name == "Bug" && x.Color == "#AA11CC");
        Assert.Contains(tags!, x => x.Id == createdB.Id && x.Name == "Feature" && x.Color == "#11CCAA");
    }

    [Fact]
    public async Task Create_ManagerAndMemberAllowed_ViewerForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-create-owner"));
        var managerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-create-manager"));
        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("tag-create-member"));
        var viewerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-create-viewer"));

        using var ownerClient = CreateClient(ownerUserId);
        using var managerClient = CreateClient(managerUserId);
        using var memberClient = CreateClient(memberUserId);
        using var viewerClient = CreateClient(viewerUserId);

        var project = await CreateProjectAsync(ownerClient, "Tag Role Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Tag Role Board");

        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = managerUserId,
                Role = ProjectRole.Manager,
                JoinedAt = DateTime.UtcNow
            });

            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = memberUserId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            });

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

        var managerCreate = await managerClient.PostAsJsonAsync($"/api/boards/{board.Id}/tags", new
        {
            name = "Manager Tag",
            color = "#223344"
        });
        Assert.Equal(HttpStatusCode.OK, managerCreate.StatusCode);

        var memberCreate = await memberClient.PostAsJsonAsync($"/api/boards/{board.Id}/tags", new
        {
            name = "Member Tag",
            color = "#445566"
        });
        Assert.Equal(HttpStatusCode.OK, memberCreate.StatusCode);

        var viewerCreate = await viewerClient.PostAsJsonAsync($"/api/boards/{board.Id}/tags", new
        {
            name = "Viewer Tag",
            color = "#778899"
        });
        Assert.Equal(HttpStatusCode.Forbidden, viewerCreate.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateNameInBoard_ReturnsConflict()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-dup-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Tag Duplicate Project");
        var board = await CreateBoardAsync(client, project.Id, "Tag Duplicate Board");
        await CreateTagAsync(client, board.Id, "Urgent", "#CC1122");

        var duplicateResponse = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tags", new
        {
            name = "  urgent  ",
            color = "#11CC22"
        });

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesTagFromAllCards()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-delete-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Tag Delete Project");
        var board = await CreateBoardAsync(client, project.Id, "Tag Delete Board");
        var tag = await CreateTagAsync(client, board.Id, "SharedTag", "#11AA22");

        var columnId = Guid.NewGuid();
        var firstCardId = Guid.NewGuid();
        var secondCardId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await _factory.WithDbContextAsync(async db =>
        {
            db.Columns.Add(new Column
            {
                Id = columnId,
                BoardId = board.Id,
                Name = "Todo",
                Position = 1000,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.Cards.Add(new Card
            {
                Id = firstCardId,
                ColumnId = columnId,
                Title = "Card A",
                Position = 1000,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = ownerUserId
            });

            db.Cards.Add(new Card
            {
                Id = secondCardId,
                ColumnId = columnId,
                Title = "Card B",
                Position = 2000,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = ownerUserId
            });

            db.CardTags.Add(new CardTag
            {
                CardId = firstCardId,
                TagId = tag.Id
            });

            db.CardTags.Add(new CardTag
            {
                CardId = secondCardId,
                TagId = tag.Id
            });

            await db.SaveChangesAsync();
        });

        var deleteResponse = await client.DeleteAsync($"/api/tags/{tag.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var remainingTag = await db.Tags.FirstOrDefaultAsync(x => x.Id == tag.Id);
            Assert.Null(remainingTag);

            var cardTagCount = await db.CardTags.CountAsync(x => x.TagId == tag.Id);
            Assert.Equal(0, cardTagCount);
        });
    }

    [Fact]
    public async Task CreateAndUpdate_InvalidHexColor_ReturnsBadRequest()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-color-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Tag Color Project");
        var board = await CreateBoardAsync(client, project.Id, "Tag Color Board");
        var validTag = await CreateTagAsync(client, board.Id, "Valid", "#123ABC");

        var invalidCreateResponse = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tags", new
        {
            name = "InvalidColorCreate",
            color = "123ABC"
        });

        var invalidUpdateResponse = await client.PutAsJsonAsync($"/api/tags/{validTag.Id}", new
        {
            color = "#12GG34"
        });

        Assert.Equal(HttpStatusCode.BadRequest, invalidCreateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdateResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateAndDelete_ManagerAndMemberAllowed_ViewerForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-update-owner"));
        var managerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-update-manager"));
        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("tag-update-member"));
        var viewerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-update-viewer"));

        using var ownerClient = CreateClient(ownerUserId);
        using var managerClient = CreateClient(managerUserId);
        using var memberClient = CreateClient(memberUserId);
        using var viewerClient = CreateClient(viewerUserId);

        var project = await CreateProjectAsync(ownerClient, "Tag Update Role Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Tag Update Board");
        var managerTag = await CreateTagAsync(ownerClient, board.Id, "ManagerTarget", "#ABCDEF");
        var memberTag = await CreateTagAsync(ownerClient, board.Id, "MemberTarget", "#FEDCBA");

        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = managerUserId,
                Role = ProjectRole.Manager,
                JoinedAt = DateTime.UtcNow
            });

            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = memberUserId,
                Role = ProjectRole.Member,
                JoinedAt = DateTime.UtcNow
            });

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

        var managerUpdate = await managerClient.PutAsJsonAsync($"/api/tags/{managerTag.Id}", new
        {
            name = "UpdatedByManager",
            color = "#A1B2C3"
        });
        Assert.Equal(HttpStatusCode.OK, managerUpdate.StatusCode);

        var memberUpdate = await memberClient.PutAsJsonAsync($"/api/tags/{memberTag.Id}", new
        {
            name = "UpdatedByMember",
            color = "#C3B2A1"
        });
        Assert.Equal(HttpStatusCode.OK, memberUpdate.StatusCode);

        var viewerUpdate = await viewerClient.PutAsJsonAsync($"/api/tags/{managerTag.Id}", new
        {
            name = "ForbiddenUpdate"
        });
        Assert.Equal(HttpStatusCode.Forbidden, viewerUpdate.StatusCode);

        var viewerDelete = await viewerClient.DeleteAsync($"/api/tags/{managerTag.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, viewerDelete.StatusCode);

        var memberDelete = await memberClient.DeleteAsync($"/api/tags/{memberTag.Id}");
        Assert.Equal(HttpStatusCode.NoContent, memberDelete.StatusCode);

        var managerDelete = await managerClient.DeleteAsync($"/api/tags/{managerTag.Id}");
        Assert.Equal(HttpStatusCode.NoContent, managerDelete.StatusCode);
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

    private static async Task<Tag> CreateTagAsync(HttpClient client, Guid boardId, string name, string color)
    {
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/tags", new { name, color });
        response.EnsureSuccessStatusCode();

        var tag = await response.Content.ReadFromJsonAsync<Tag>();
        Assert.NotNull(tag);
        return tag!;
    }
}
