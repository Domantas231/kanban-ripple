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
    public async Task ListByProject_ReturnsAllTags()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-list-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Tag List Project");
        var createdA = await CreateTagAsync(client, project.Id, "Bug", "#AA11CC");
        var createdB = await CreateTagAsync(client, project.Id, "Feature", "#11CCAA");

        var response = await client.GetAsync($"/api/projects/{project.Id}/tags");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tags = await response.Content.ReadFromJsonAsync<List<Tag>>();
        Assert.NotNull(tags);
        Assert.Contains(tags!, x => x.Id == createdA.Id && x.Name == "Bug" && x.Color == "#AA11CC");
        Assert.Contains(tags!, x => x.Id == createdB.Id && x.Name == "Feature" && x.Color == "#11CCAA");
    }

    [Fact]
    public async Task Create_ModeratorAllowed_MemberForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-create-owner"));
        var moderatorUserId = await _factory.CreateUserAsync(UniqueEmail("tag-create-moderator"));
        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("tag-create-member"));

        using var ownerClient = CreateClient(ownerUserId);
        using var moderatorClient = CreateClient(moderatorUserId);
        using var memberClient = CreateClient(memberUserId);

        var project = await CreateProjectAsync(ownerClient, "Tag Role Project");

        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = moderatorUserId,
                Role = ProjectRole.Moderator,
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

            await db.SaveChangesAsync();
        });

        var moderatorCreate = await moderatorClient.PostAsJsonAsync($"/api/projects/{project.Id}/tags", new
        {
            name = "Moderator Tag",
            color = "#223344"
        });
        Assert.Equal(HttpStatusCode.OK, moderatorCreate.StatusCode);

        var memberCreate = await memberClient.PostAsJsonAsync($"/api/projects/{project.Id}/tags", new
        {
            name = "Member Tag",
            color = "#445566"
        });
        Assert.Equal(HttpStatusCode.Forbidden, memberCreate.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateNameInProject_ReturnsConflict()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-dup-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Tag Duplicate Project");
        await CreateTagAsync(client, project.Id, "Urgent", "#CC1122");

        var duplicateResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/tags", new
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
        var tag = await CreateTagAsync(client, project.Id, "SharedTag", "#11AA22");

        var boardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var firstCardId = Guid.NewGuid();
        var secondCardId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await _factory.WithDbContextAsync(async db =>
        {
            db.Boards.Add(new Board
            {
                Id = boardId,
                ProjectId = project.Id,
                Name = "Tag Delete Board",
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
        var validTag = await CreateTagAsync(client, project.Id, "Valid", "#123ABC");

        var invalidCreateResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/tags", new
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
    public async Task UpdateAndDelete_ModeratorAllowed_MemberForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("tag-update-owner"));
        var moderatorUserId = await _factory.CreateUserAsync(UniqueEmail("tag-update-moderator"));
        var memberUserId = await _factory.CreateUserAsync(UniqueEmail("tag-update-member"));

        using var ownerClient = CreateClient(ownerUserId);
        using var moderatorClient = CreateClient(moderatorUserId);
        using var memberClient = CreateClient(memberUserId);

        var project = await CreateProjectAsync(ownerClient, "Tag Update Role Project");
        var tag = await CreateTagAsync(ownerClient, project.Id, "ToUpdate", "#ABCDEF");

        await _factory.WithDbContextAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = moderatorUserId,
                Role = ProjectRole.Moderator,
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

            await db.SaveChangesAsync();
        });

        var moderatorUpdate = await moderatorClient.PutAsJsonAsync($"/api/tags/{tag.Id}", new
        {
            name = "UpdatedByModerator",
            color = "#A1B2C3"
        });
        Assert.Equal(HttpStatusCode.OK, moderatorUpdate.StatusCode);

        var memberUpdate = await memberClient.PutAsJsonAsync($"/api/tags/{tag.Id}", new
        {
            name = "ForbiddenUpdate"
        });
        Assert.Equal(HttpStatusCode.Forbidden, memberUpdate.StatusCode);

        var memberDelete = await memberClient.DeleteAsync($"/api/tags/{tag.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, memberDelete.StatusCode);

        var moderatorDelete = await moderatorClient.DeleteAsync($"/api/tags/{tag.Id}");
        Assert.Equal(HttpStatusCode.NoContent, moderatorDelete.StatusCode);
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

    private static async Task<Tag> CreateTagAsync(HttpClient client, Guid projectId, string name, string color)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/tags", new { name, color });
        response.EnsureSuccessStatusCode();

        var tag = await response.Content.ReadFromJsonAsync<Tag>();
        Assert.NotNull(tag);
        return tag!;
    }
}