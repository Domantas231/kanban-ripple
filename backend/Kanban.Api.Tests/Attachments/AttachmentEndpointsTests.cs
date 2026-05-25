using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Services.Projects;
using Kanban.Api.Tests.Projects;

namespace Kanban.Api.Tests.Attachments;

public sealed class AttachmentEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public AttachmentEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Upload_ValidFile_ReturnsAttachment()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("attach-upload"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Attachment Upload Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");

        var response = await UploadFileAsync(client, card.Id, "test.pdf", "application/pdf", 1024);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var attachment = await response.Content.ReadFromJsonAsync<Attachment>();
        Assert.NotNull(attachment);
        Assert.Equal("test.pdf", attachment!.Filename);
        Assert.Equal(1024, attachment.FileSize);
        Assert.Equal(card.Id, attachment.CardId);
        Assert.Equal(userId, attachment.UploadedBy);
    }

    [Fact]
    public async Task Upload_FileExceeds25MB_ReturnsBadRequest()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("attach-too-large"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Attachment Size Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");

        var response = await UploadFileAsync(client, card.Id, "large.pdf", "application/pdf", 26 * 1024 * 1024);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_FileAt25MB_Succeeds()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("attach-25mb"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Attachment 25MB Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");

        var response = await UploadFileAsync(client, card.Id, "max.pdf", "application/pdf", 25 * 1024 * 1024);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ExceedsPerCardTotalSize_ReturnsBadRequest()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("attach-card-total"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Card Total Size Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");

        // 4 files * 25 MB = 100 MB already on the card.
        for (var i = 0; i < 4; i++)
        {
            var uploadResponse = await UploadFileAsync(client, card.Id, $"chunk{i}.pdf", "application/pdf", 25 * 1024 * 1024);
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        }

        // Any additional non-zero file should push the card over 100 MB.
        var response = await UploadFileAsync(client, card.Id, "over.txt", "text/plain", 1024);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ExecutableFile_ReturnsBadRequest()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("attach-exe"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Attachment Exe Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");

        var response = await UploadFileAsync(client, card.Id, "malware.exe", "application/octet-stream", 512);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ExceedsPerProjectLimit_ReturnsBadRequest()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("attach-limit"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Attachment Limit Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");

        for (var i = 0; i < 20; i++)
        {
            var uploadResponse = await UploadFileAsync(client, card.Id, $"file{i}.txt", "text/plain", 100);
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        }

        var response = await UploadFileAsync(client, card.Id, "file21.txt", "text/plain", 100);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDownloadUrl_ValidAttachment_ReturnsUrl()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("attach-download"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Attachment Download Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");

        var uploadResponse = await UploadFileAsync(client, card.Id, "doc.pdf", "application/pdf", 512);
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<Attachment>();
        Assert.NotNull(attachment);

        var response = await client.GetAsync($"/api/attachments/{attachment!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DownloadUrlResponse>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result!.Url));
        Assert.Equal("doc.pdf", result.Filename);
    }

    [Fact]
    public async Task Delete_ValidAttachment_ReturnsNoContent()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("attach-delete"));
        using var client = CreateClient(userId);

        var project = await CreateProjectAsync(client, "Attachment Delete Project");
        var board = await CreateBoardAsync(client, project.Id, "Board");
        var column = await CreateColumnAsync(client, board.Id, "Todo");
        var card = await CreateCardAsync(client, column.Id, "Card");

        var uploadResponse = await UploadFileAsync(client, card.Id, "remove-me.txt", "text/plain", 256);
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<Attachment>();
        Assert.NotNull(attachment);

        var response = await client.DeleteAsync($"/api/attachments/{attachment!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync($"/api/attachments/{attachment.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Upload_AsViewer_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("attach-viewer-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Viewer Attach Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");
        var card = await CreateCardAsync(ownerClient, column.Id, "Card");

        var viewerUserId = await _factory.CreateUserAsync(UniqueEmail("attach-viewer-user"));
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

        var response = await UploadFileAsync(viewerClient, card.Id, "nope.txt", "text/plain", 100);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDownloadUrl_AsViewer_ReturnsOk()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("attach-viewer-dl-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Viewer Download Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");
        var card = await CreateCardAsync(ownerClient, column.Id, "Card");

        var uploadResponse = await UploadFileAsync(ownerClient, card.Id, "shared.pdf", "application/pdf", 512);
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<Attachment>();
        Assert.NotNull(attachment);

        var viewerUserId = await _factory.CreateUserAsync(UniqueEmail("attach-viewer-dl-user"));
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

        var response = await viewerClient.GetAsync($"/api/attachments/{attachment!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Upload_NonExistentCard_ReturnsNotFound()
    {
        var userId = await _factory.CreateUserAsync(UniqueEmail("attach-nocard"));
        using var client = CreateClient(userId);

        var response = await UploadFileAsync(client, Guid.NewGuid(), "ghost.txt", "text/plain", 100);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ByOtherMember_NotUploaderOrCreator_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("attach-delete-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Attach Delete Forbidden Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");

        // Card is created by a member (so neither uploader nor card creator is the third user)
        var creatorUserId = await _factory.CreateUserAsync(UniqueEmail("attach-delete-creator"));
        using var creatorClient = CreateClient(creatorUserId);
        await AddProjectMemberAsync(project.Id, creatorUserId, ProjectRole.Member);
        var card = await CreateCardAsync(creatorClient, column.Id, "Card");

        // Uploader uploads the file (also a Member)
        var uploaderUserId = await _factory.CreateUserAsync(UniqueEmail("attach-delete-uploader"));
        await AddProjectMemberAsync(project.Id, uploaderUserId, ProjectRole.Member);
        using var uploaderClient = CreateClient(uploaderUserId);
        var uploadResponse = await UploadFileAsync(uploaderClient, card.Id, "doc.txt", "text/plain", 200);
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<Attachment>();
        Assert.NotNull(attachment);

        // Another member who is neither uploader, card creator, nor manager
        var otherUserId = await _factory.CreateUserAsync(UniqueEmail("attach-delete-other"));
        await AddProjectMemberAsync(project.Id, otherUserId, ProjectRole.Member);
        using var otherClient = CreateClient(otherUserId);

        var response = await otherClient.DeleteAsync($"/api/attachments/{attachment!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ByCardCreator_ReturnsNoContent()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("attach-delete-creator-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Card Creator Delete Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");

        var creatorUserId = await _factory.CreateUserAsync(UniqueEmail("attach-delete-creator-user"));
        await AddProjectMemberAsync(project.Id, creatorUserId, ProjectRole.Member);
        using var creatorClient = CreateClient(creatorUserId);
        var card = await CreateCardAsync(creatorClient, column.Id, "Card");

        // Different uploader
        var uploaderUserId = await _factory.CreateUserAsync(UniqueEmail("attach-delete-creator-uploader"));
        await AddProjectMemberAsync(project.Id, uploaderUserId, ProjectRole.Member);
        using var uploaderClient = CreateClient(uploaderUserId);
        var uploadResponse = await UploadFileAsync(uploaderClient, card.Id, "doc.txt", "text/plain", 200);
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<Attachment>();
        Assert.NotNull(attachment);

        // Card creator removes — allowed
        var response = await creatorClient.DeleteAsync($"/api/attachments/{attachment!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ByProjectManager_ReturnsNoContent()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("attach-delete-mgr-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var project = await CreateProjectAsync(ownerClient, "Manager Delete Project");
        var board = await CreateBoardAsync(ownerClient, project.Id, "Board");
        var column = await CreateColumnAsync(ownerClient, board.Id, "Todo");

        var creatorUserId = await _factory.CreateUserAsync(UniqueEmail("attach-delete-mgr-creator"));
        await AddProjectMemberAsync(project.Id, creatorUserId, ProjectRole.Member);
        using var creatorClient = CreateClient(creatorUserId);
        var card = await CreateCardAsync(creatorClient, column.Id, "Card");

        var uploaderUserId = await _factory.CreateUserAsync(UniqueEmail("attach-delete-mgr-uploader"));
        await AddProjectMemberAsync(project.Id, uploaderUserId, ProjectRole.Member);
        using var uploaderClient = CreateClient(uploaderUserId);
        var uploadResponse = await UploadFileAsync(uploaderClient, card.Id, "doc.txt", "text/plain", 200);
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<Attachment>();
        Assert.NotNull(attachment);

        var managerUserId = await _factory.CreateUserAsync(UniqueEmail("attach-delete-mgr-user"));
        await AddProjectMemberAsync(project.Id, managerUserId, ProjectRole.Manager);
        using var managerClient = CreateClient(managerUserId);

        var response = await managerClient.DeleteAsync($"/api/attachments/{attachment!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
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

    private static async Task<HttpResponseMessage> UploadFileAsync(
        HttpClient client,
        Guid cardId,
        string filename,
        string contentType,
        int sizeInBytes)
    {
        var content = new MultipartFormDataContent();
        var fileBytes = new byte[sizeInBytes];
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", filename);

        return await client.PostAsync($"/api/cards/{cardId}/attachments", content);
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

    private sealed record DownloadUrlResponse(string Url, string Filename);
}
