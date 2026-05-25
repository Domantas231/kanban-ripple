using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Tests.Projects;

namespace Kanban.Api.Tests.Comments;

public sealed class CommentEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public CommentEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateComment_ReturnsOk_WithAuthorPopulated()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("comment-create-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Comment Create Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card With Comments");

        var response = await client.PostAsJsonAsync($"/api/cards/{card.Id}/comments", new
        {
            content = "This is a comment"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<Comment>();
        Assert.NotNull(created);
        Assert.Equal(card.Id, created!.CardId);
        Assert.Equal(ownerUserId, created.AuthorId);
        Assert.Equal("This is a comment", created.Content);
        Assert.NotNull(created.Author);
    }

    [Fact]
    public async Task ListComments_ReturnsAll_InChronologicalOrder()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("comment-list-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Comment List Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");

        await PostCommentAsync(client, card.Id, "First comment");
        await PostCommentAsync(client, card.Id, "Second comment");
        await PostCommentAsync(client, card.Id, "Third comment");

        var response = await client.GetAsync($"/api/cards/{card.Id}/comments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var comments = await response.Content.ReadFromJsonAsync<List<Comment>>();
        Assert.NotNull(comments);
        Assert.Equal(3, comments!.Count);
        Assert.Equal("First comment", comments[0].Content);
        Assert.Equal("Second comment", comments[1].Content);
        Assert.Equal("Third comment", comments[2].Content);
    }

    [Fact]
    public async Task UpdateComment_ByAuthor_ReturnsOk()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("comment-update-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Comment Update Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");
        var comment = await PostCommentAsync(client, card.Id, "Original content");

        var response = await client.PutAsJsonAsync($"/api/comments/{comment.Id}", new
        {
            content = "Updated content"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<Comment>();
        Assert.NotNull(updated);
        Assert.Equal("Updated content", updated!.Content);
    }

    [Fact]
    public async Task UpdateComment_ByNonAuthor_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("comment-update-author"));
        var otherUserId = await _factory.CreateUserAsync(UniqueEmail("comment-update-other"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Comment Auth Project");
        await AddProjectMemberAsync(project.Id, otherUserId, ProjectRole.Member);

        var board = await CreateBoardAsync(ownerClient, project.Id, "Main Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");
        var card = await CreateCardAsync(ownerClient, column.Id, "Card");
        var comment = await PostCommentAsync(ownerClient, card.Id, "Owner's comment");

        using var otherClient = CreateClient(otherUserId);
        var response = await otherClient.PutAsJsonAsync($"/api/comments/{comment.Id}", new
        {
            content = "Attempted edit"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_ByAuthor_ReturnsNoContent()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("comment-delete-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Comment Delete Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");
        var comment = await PostCommentAsync(client, card.Id, "Delete me");

        var deleteResponse = await client.DeleteAsync($"/api/comments/{comment.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await client.GetAsync($"/api/cards/{card.Id}/comments");
        var comments = await listResponse.Content.ReadFromJsonAsync<List<Comment>>();
        Assert.NotNull(comments);
        Assert.Empty(comments!);
    }

    [Fact]
    public async Task CreateComment_WithEmptyContent_ReturnsBadRequest()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("comment-empty-owner"));
        using var client = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(client, "Comment Empty Project");
        var board = await CreateBoardAsync(client, project.Id, "Main Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");

        var response = await client.PostAsJsonAsync($"/api/cards/{card.Id}/comments", new
        {
            content = "   "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateComment_OnNonExistentCard_ReturnsNotFound()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("comment-notfound-owner"));
        using var client = CreateClient(ownerUserId);

        var response = await client.PostAsJsonAsync($"/api/cards/{Guid.NewGuid()}/comments", new
        {
            content = "Test"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private static async Task<Comment> PostCommentAsync(HttpClient client, Guid cardId, string content)
    {
        var response = await client.PostAsJsonAsync($"/api/cards/{cardId}/comments", new { content });
        response.EnsureSuccessStatusCode();

        var comment = await response.Content.ReadFromJsonAsync<Comment>();
        Assert.NotNull(comment);
        return comment!;
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
}
