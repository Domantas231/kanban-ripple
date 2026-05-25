using Kanban.Api.Configuration.Options;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Email;
using Kanban.Api.Services.Invitations;
using Kanban.Api.Services.Projects;
using Kanban.Api.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kanban.Api.Tests.Invitations;

public class InvitationServicePropertyTests
{
    [Fact]
    public async Task Property_19_InvitationTriggersEmailSend()
    {
        for (var i = 0; i < 12; i++)
        {
            var fixture = CreateFixture();
            var inviterId = Guid.NewGuid();
            var inviterEmail = $"inviter{i}.{Guid.NewGuid():N}@example.com";
            var invitedEmail = $"invitee{i}.{Guid.NewGuid():N}@example.com";

            AddUser(fixture.DbContext, inviterId, inviterEmail);
            var project = await fixture.ProjectService.CreateAsync(inviterId, $"Project-{i}");

            var token = await fixture.InvitationService.CreateInvitationAsync(project.Id, inviterId, invitedEmail);

            var sent = Assert.Single(fixture.EmailService.SentEmails);
            Assert.Equal(invitedEmail, sent.ToEmail);
            Assert.Contains("invited", sent.Subject, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("token=", sent.Body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(token, sent.Body, StringComparison.Ordinal);

            var invitation = await fixture.DbContext.Invitations.SingleAsync(x => x.Token == token);
            Assert.Equal(project.Id, invitation.ProjectId);
            Assert.Equal(inviterId, invitation.InvitedBy);
            Assert.Equal(invitedEmail, invitation.Email);
            Assert.Null(invitation.AcceptedAt);
            Assert.Null(invitation.AcceptedBy);
            Assert.True(invitation.ExpiresAt > invitation.CreatedAt.AddDays(6));
            Assert.True(invitation.ExpiresAt <= invitation.CreatedAt.AddDays(7).AddMinutes(1));
        }
    }

    [Fact]
    public async Task Property_20_AcceptAddsMemberAndGrantsProjectAccess()
    {
        for (var i = 0; i < 12; i++)
        {
            var fixture = CreateFixture();
            var inviterId = Guid.NewGuid();
            var invitedUserId = Guid.NewGuid();
            var inviterEmail = $"owner{i}.{Guid.NewGuid():N}@example.com";
            var invitedEmail = $"new-member{i}.{Guid.NewGuid():N}@example.com";

            AddUser(fixture.DbContext, inviterId, inviterEmail);
            AddUser(fixture.DbContext, invitedUserId, invitedEmail);

            var project = await fixture.ProjectService.CreateAsync(inviterId, $"Team-{i}");

            var hasAccessBefore = await fixture.ProjectService.CheckAccessAsync(project.Id, invitedUserId, ProjectRole.Viewer);
            Assert.False(hasAccessBefore);

            var token = await fixture.InvitationService.CreateInvitationAsync(project.Id, inviterId, invitedEmail);
            await fixture.InvitationService.AcceptInvitationAsync(token, invitedUserId);

            var membership = await fixture.DbContext.ProjectMembers
                .SingleAsync(x => x.ProjectId == project.Id && x.UserId == invitedUserId);

            Assert.Equal(ProjectRole.Member, membership.Role);

            var hasAccessAfter = await fixture.ProjectService.CheckAccessAsync(project.Id, invitedUserId, ProjectRole.Viewer);
            Assert.True(hasAccessAfter);

            var acceptedInvitation = await fixture.DbContext.Invitations.SingleAsync(x => x.Token == token);
            Assert.Equal(invitedUserId, acceptedInvitation.AcceptedBy);
            Assert.NotNull(acceptedInvitation.AcceptedAt);
        }
    }

    [Fact]
    public async Task CreateInvitation_BlankEmail_Throws()
    {
        var fixture = CreateFixture();
        var inviterId = Guid.NewGuid();
        AddUser(fixture.DbContext, inviterId, $"inviter.{Guid.NewGuid():N}@example.com");
        var project = await fixture.ProjectService.CreateAsync(inviterId, "Project");

        await Assert.ThrowsAsync<Kanban.Api.Exceptions.BadRequestException>(() =>
            fixture.InvitationService.CreateInvitationAsync(project.Id, inviterId, "   "));
    }

    [Fact]
    public async Task CreateInvitation_NonManagerInviter_ThrowsForbidden()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        AddUser(fixture.DbContext, ownerId, $"owner.{Guid.NewGuid():N}@example.com");
        AddUser(fixture.DbContext, memberId, $"member.{Guid.NewGuid():N}@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Project");
        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = memberId,
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<Kanban.Api.Exceptions.ForbiddenException>(() =>
            fixture.InvitationService.CreateInvitationAsync(project.Id, memberId, "new@example.com"));
    }

    [Fact]
    public async Task CreateInvitation_NotProjectMember_ThrowsForbidden()
    {
        var fixture = CreateFixture();
        var outsiderId = Guid.NewGuid();
        AddUser(fixture.DbContext, outsiderId, $"outsider.{Guid.NewGuid():N}@example.com");

        await Assert.ThrowsAsync<Kanban.Api.Exceptions.ForbiddenException>(() =>
            fixture.InvitationService.CreateInvitationAsync(Guid.NewGuid(), outsiderId, "new@example.com"));
    }

    [Fact]
    public async Task CreateInvitation_DuplicateMemberEmail_ThrowsConflict()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var existingMemberId = Guid.NewGuid();
        var existingEmail = $"existing.{Guid.NewGuid():N}@example.com";
        AddUser(fixture.DbContext, ownerId, $"owner.{Guid.NewGuid():N}@example.com");
        AddUser(fixture.DbContext, existingMemberId, existingEmail);

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Project");
        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = existingMemberId,
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<Kanban.Api.Exceptions.ConflictException>(() =>
            fixture.InvitationService.CreateInvitationAsync(project.Id, ownerId, existingEmail));
    }

    [Fact]
    public async Task AcceptInvitation_ExpiredToken_ThrowsBadRequest()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var invitedId = Guid.NewGuid();
        var invitedEmail = $"invited.{Guid.NewGuid():N}@example.com";
        AddUser(fixture.DbContext, ownerId, $"owner.{Guid.NewGuid():N}@example.com");
        AddUser(fixture.DbContext, invitedId, invitedEmail);

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Project");
        var token = await fixture.InvitationService.CreateInvitationAsync(project.Id, ownerId, invitedEmail);

        var inv = await fixture.DbContext.Invitations.SingleAsync(x => x.Token == token);
        inv.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await fixture.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<Kanban.Api.Exceptions.BadRequestException>(() =>
            fixture.InvitationService.AcceptInvitationAsync(token, invitedId));
    }

    [Fact]
    public async Task AcceptInvitation_AlreadyAccepted_ThrowsBadRequest()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var invitedId = Guid.NewGuid();
        var invitedEmail = $"invited.{Guid.NewGuid():N}@example.com";
        AddUser(fixture.DbContext, ownerId, $"owner.{Guid.NewGuid():N}@example.com");
        AddUser(fixture.DbContext, invitedId, invitedEmail);

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Project");
        var token = await fixture.InvitationService.CreateInvitationAsync(project.Id, ownerId, invitedEmail);

        await fixture.InvitationService.AcceptInvitationAsync(token, invitedId);

        await Assert.ThrowsAsync<Kanban.Api.Exceptions.BadRequestException>(() =>
            fixture.InvitationService.AcceptInvitationAsync(token, invitedId));
    }

    [Fact]
    public async Task AcceptInvitation_UnknownUserId_ThrowsForbidden()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        AddUser(fixture.DbContext, ownerId, $"owner.{Guid.NewGuid():N}@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Project");
        var token = await fixture.InvitationService.CreateInvitationAsync(project.Id, ownerId, "stranger@example.com");

        await Assert.ThrowsAsync<Kanban.Api.Exceptions.ForbiddenException>(() =>
            fixture.InvitationService.AcceptInvitationAsync(token, Guid.NewGuid()));
    }

    [Fact]
    public async Task AcceptInvitation_SoftDeletedProject_ThrowsNotFound()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var invitedId = Guid.NewGuid();
        var invitedEmail = $"invited.{Guid.NewGuid():N}@example.com";
        AddUser(fixture.DbContext, ownerId, $"owner.{Guid.NewGuid():N}@example.com");
        AddUser(fixture.DbContext, invitedId, invitedEmail);

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Project");
        var token = await fixture.InvitationService.CreateInvitationAsync(project.Id, ownerId, invitedEmail);

        var stored = await fixture.DbContext.Projects.SingleAsync(x => x.Id == project.Id);
        stored.DeletedAt = DateTime.UtcNow;
        await fixture.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<Kanban.Api.Exceptions.NotFoundException>(() =>
            fixture.InvitationService.AcceptInvitationAsync(token, invitedId));
    }

    [Fact]
    public async Task AcceptInvitation_AlreadyMember_MarksAcceptedWithoutDuplicateMembership()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var memberEmail = $"member.{Guid.NewGuid():N}@example.com";
        AddUser(fixture.DbContext, ownerId, $"owner.{Guid.NewGuid():N}@example.com");
        AddUser(fixture.DbContext, memberId, memberEmail);

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Project");

        // Pre-populate membership directly (bypassing the conflict guard in CreateInvitationAsync).
        fixture.DbContext.Invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Email = memberEmail,
            Token = "manual-token-" + Guid.NewGuid().ToString("N"),
            InvitedBy = ownerId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        fixture.DbContext.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = memberId,
            Role = ProjectRole.Member,
            JoinedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        var inv = await fixture.DbContext.Invitations.SingleAsync(x => x.Email == memberEmail);
        await fixture.InvitationService.AcceptInvitationAsync(inv.Token, memberId);

        var memberships = await fixture.DbContext.ProjectMembers
            .Where(x => x.ProjectId == project.Id && x.UserId == memberId)
            .ToListAsync();
        Assert.Single(memberships);

        var refreshed = await fixture.DbContext.Invitations.SingleAsync(x => x.Token == inv.Token);
        Assert.NotNull(refreshed.AcceptedAt);
        Assert.Equal(memberId, refreshed.AcceptedBy);
    }

    [Fact]
    public async Task IsValidTokenAsync_ReturnsExpectedResults()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        AddUser(fixture.DbContext, ownerId, $"owner.{Guid.NewGuid():N}@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Project");
        var token = await fixture.InvitationService.CreateInvitationAsync(project.Id, ownerId, "x@example.com");

        Assert.True(await fixture.InvitationService.IsValidTokenAsync(token));
        Assert.False(await fixture.InvitationService.IsValidTokenAsync("   "));
        Assert.False(await fixture.InvitationService.IsValidTokenAsync("does-not-exist"));

        var stored = await fixture.DbContext.Invitations.SingleAsync(x => x.Token == token);
        stored.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await fixture.DbContext.SaveChangesAsync();
        Assert.False(await fixture.InvitationService.IsValidTokenAsync(token));
    }

    [Fact]
    public async Task GetByTokenAsync_ReturnsInvitationOrThrowsNotFound()
    {
        var fixture = CreateFixture();
        var ownerId = Guid.NewGuid();
        AddUser(fixture.DbContext, ownerId, $"owner.{Guid.NewGuid():N}@example.com");

        var project = await fixture.ProjectService.CreateAsync(ownerId, "Project");
        var token = await fixture.InvitationService.CreateInvitationAsync(project.Id, ownerId, "x@example.com");

        var fetched = await fixture.InvitationService.GetByTokenAsync(token);
        Assert.Equal(token, fetched.Token);
        Assert.Equal(project.Id, fetched.ProjectId);

        await Assert.ThrowsAsync<Kanban.Api.Exceptions.NotFoundException>(() =>
            fixture.InvitationService.GetByTokenAsync("   "));
        await Assert.ThrowsAsync<Kanban.Api.Exceptions.NotFoundException>(() =>
            fixture.InvitationService.GetByTokenAsync("nope"));
    }

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"invitation-property-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();

        var frontendOptions = Options.Create(new FrontendOptions
        {
            Url = "http://localhost:5173",
            InvitationAcceptUrl = "http://localhost:5173/invitations/accept"
        });
        var invitationOptions = Options.Create(new InvitationOptions());

        var emailService = new RecordingEmailService();
        var invitationService = new InvitationService(dbContext, emailService, frontendOptions, invitationOptions, NullLogger<InvitationService>.Instance);
        var projectService = TestServiceBuilder.BuildProjectService(dbContext);

        return new TestFixture(dbContext, invitationService, projectService, emailService);
    }

    private static void AddUser(ApplicationDbContext dbContext, Guid userId, string email)
    {
        dbContext.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            NormalizedUserName = email.ToUpperInvariant(),
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        dbContext.SaveChanges();
    }

    private sealed record TestFixture(
        ApplicationDbContext DbContext,
        InvitationService InvitationService,
        ProjectService ProjectService,
        RecordingEmailService EmailService);

    private sealed class RecordingEmailService : IEmailService
    {
        public List<SentEmail> SentEmails { get; } = [];

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        {
            SentEmails.Add(new SentEmail(toEmail, subject, body));
            return Task.CompletedTask;
        }

        public Task SendAsync(string toEmail, string subject, EmailBody body, CancellationToken cancellationToken = default)
        {
            SentEmails.Add(new SentEmail(toEmail, subject, body.PlainText));
            return Task.CompletedTask;
        }
    }

    private sealed record SentEmail(string ToEmail, string Subject, string Body);
}
