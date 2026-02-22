using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Email;
using Kanban.Api.Services.Invitations;
using Kanban.Api.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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
            var project = await fixture.ProjectService.CreateAsync(inviterId, $"Project-{i}", ProjectType.Team);

            var token = await fixture.InvitationService.CreateInvitationAsync(project.Id, inviterId, invitedEmail);

            var sent = Assert.Single(fixture.EmailService.SentEmails);
            Assert.Equal(invitedEmail, sent.ToEmail);
            Assert.Contains("invitation", sent.Subject, StringComparison.OrdinalIgnoreCase);
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

            var project = await fixture.ProjectService.CreateAsync(inviterId, $"Team-{i}", ProjectType.Team);

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

    private static TestFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"invitation-property-tests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:Url"] = "http://localhost:5173",
                ["Frontend:InvitationAcceptUrl"] = "http://localhost:5173/invitations/accept"
            })
            .Build();

        var emailService = new RecordingEmailService();
        var invitationService = new InvitationService(dbContext, emailService, configuration);
        var projectService = new ProjectService(dbContext);

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
    }

    private sealed record SentEmail(string ToEmail, string Subject, string Body);
}
