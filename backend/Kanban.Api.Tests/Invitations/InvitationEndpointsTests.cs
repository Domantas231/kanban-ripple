using System.Net;
using System.Net.Http.Json;
using Kanban.Api.Models;
using Kanban.Api.Tests.Projects;

namespace Kanban.Api.Tests.Invitations;

public sealed class InvitationEndpointsTests : IClassFixture<ProjectsApiFactory>
{
    private readonly ProjectsApiFactory _factory;

    public InvitationEndpointsTests(ProjectsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Invite_AsOwner_ReturnsOk_AndPersistsInvitation()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("invite-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var createProject = await ownerClient.PostAsJsonAsync("/api/projects", new { name = "Invite Project" });
        createProject.EnsureSuccessStatusCode();
        var project = await createProject.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var invitedEmail = UniqueEmail("invite-target");
        var inviteResponse = await ownerClient.PostAsJsonAsync($"/api/projects/{project!.Id}/invite", new { email = invitedEmail });

        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var invitation = db.Invitations.SingleOrDefault(x => x.ProjectId == project.Id && x.Email == invitedEmail);
            Assert.NotNull(invitation);
            Assert.Equal(ownerUserId, invitation!.InvitedBy);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Invite_AsViewer_ReturnsForbidden()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("invite-owner-viewer"));
        using var ownerClient = CreateClient(ownerUserId);

        var createProject = await ownerClient.PostAsJsonAsync("/api/projects", new { name = "Invite Access Project" });
        createProject.EnsureSuccessStatusCode();
        var project = await createProject.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var viewerUserId = await _factory.CreateUserAsync(UniqueEmail("invite-viewer"));
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

        var response = await viewerClient.PostAsJsonAsync($"/api/projects/{project!.Id}/invite", new
        {
            email = UniqueEmail("invite-nope")
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Accept_ValidInvitation_ReturnsNoContent_AndAddsMembership()
    {
        var ownerUserId = await _factory.CreateUserAsync(UniqueEmail("accept-owner"));
        using var ownerClient = CreateClient(ownerUserId);

        var createProject = await ownerClient.PostAsJsonAsync("/api/projects", new { name = "Accept Project" });
        createProject.EnsureSuccessStatusCode();
        var project = await createProject.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);

        var inviteeEmail = UniqueEmail("accept-invitee");
        var inviteeUserId = await _factory.CreateUserAsync(inviteeEmail);
        using var inviteeClient = CreateClient(inviteeUserId);

        var inviteResponse = await ownerClient.PostAsJsonAsync($"/api/projects/{project!.Id}/invite", new { email = inviteeEmail });
        inviteResponse.EnsureSuccessStatusCode();

        string? token = null;
        await _factory.WithDbContextAsync(async db =>
        {
            token = db.Invitations
                .Where(x => x.ProjectId == project.Id && x.Email == inviteeEmail)
                .Select(x => x.Token)
                .SingleOrDefault();

            await Task.CompletedTask;
        });

        Assert.False(string.IsNullOrWhiteSpace(token));

        var acceptResponse = await inviteeClient.PostAsync($"/api/invitations/{Uri.EscapeDataString(token!)}/accept", content: null);
        Assert.Equal(HttpStatusCode.NoContent, acceptResponse.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var membership = db.ProjectMembers.SingleOrDefault(x => x.ProjectId == project.Id && x.UserId == inviteeUserId);
            Assert.NotNull(membership);
            Assert.Equal(ProjectRole.Member, membership!.Role);

            var invitation = db.Invitations.SingleOrDefault(x => x.Token == token);
            Assert.NotNull(invitation);
            Assert.Equal(inviteeUserId, invitation!.AcceptedBy);
            Assert.NotNull(invitation.AcceptedAt);

            await Task.CompletedTask;
        });
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
}