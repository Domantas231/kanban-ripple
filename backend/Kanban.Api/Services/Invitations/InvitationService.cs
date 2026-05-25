using System.Security.Cryptography;
using Kanban.Api.Configuration.Options;
using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kanban.Api.Services.Invitations;

public sealed class InvitationService : IInvitationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<InvitationService> _logger;
    private readonly string _acceptInvitationUrlBase;
    private readonly int _invitationLifetimeDays;

    public InvitationService(
        ApplicationDbContext dbContext,
        IEmailService emailService,
        IOptions<FrontendOptions> frontendOptions,
        IOptions<InvitationOptions> invitationOptions,
        ILogger<InvitationService> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;

        _acceptInvitationUrlBase = frontendOptions.Value.ResolvedInvitationAcceptUrl;
        _invitationLifetimeDays = invitationOptions.Value.LifetimeDays;
    }

    public async Task<string> CreateInvitationAsync(Guid projectId, Guid invitedBy, string email, ProjectRole role = ProjectRole.Member)
    {
        var normalizedEmail = email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new BadRequestException("Invitation email is required.");
        }

        if (role != ProjectRole.Manager && role != ProjectRole.Member && role != ProjectRole.Viewer)
        {
            throw new BadRequestException("Role must be Manager, Member, or Viewer.");
        }

        var membership = await _dbContext.ProjectMembers
            .Where(x => x.ProjectId == projectId && x.UserId == invitedBy)
            .Select(x => (ProjectRole?)x.Role)
            .FirstOrDefaultAsync();

        if (membership is null || !HasRequiredRole(membership.Value, ProjectRole.Manager))
        {
            throw new ForbiddenException("Forbidden.");
        }

        if (role == ProjectRole.Manager && membership.Value != ProjectRole.Owner)
        {
            throw new ForbiddenException("Only the workspace owner can invite managers.");
        }

        var project = await _dbContext.Projects.FirstOrDefaultAsync(x => x.Id == projectId);
        if (project is null)
        {
            throw new NotFoundException("Project not found.");
        }

        var existingMemberByEmail = await _dbContext.ProjectMembers
            .Where(x => x.ProjectId == projectId)
            .Include(x => x.User)
            .AnyAsync(x => x.User.Email != null && x.User.Email.ToLower() == normalizedEmail.ToLower());

        if (existingMemberByEmail)
        {
            throw new ConflictException("User is already a project member.", "ALREADY_MEMBER");
        }

        var token = GenerateSecureToken();
        var now = DateTime.UtcNow;

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Email = normalizedEmail,
            Token = token,
            Role = role,
            InvitedBy = invitedBy,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_invitationLifetimeDays)
        };

        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        var inviter = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == invitedBy);
        var inviterName = inviter?.UserName ?? inviter?.Email ?? "A team member";

        var invitationUrl = $"{_acceptInvitationUrlBase}?token={Uri.EscapeDataString(token)}";
        _logger.LogInformation("Invitation created: Email={Email}, Project={Project}, URL={InvitationUrl}", normalizedEmail, project.Name, invitationUrl);
        var emailBody = EmailTemplates.ProjectInvitation(invitationUrl, project.Name, inviterName, _invitationLifetimeDays);
        await _emailService.SendAsync(normalizedEmail, $"You've been invited to join {project.Name} on Kanban Ripple", emailBody);

        return token;
    }

    public async Task AcceptInvitationAsync(string token, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new BadRequestException("Invalid or expired invitation token.");
        }

        var invitation = await _dbContext.Invitations
            .FirstOrDefaultAsync(x => x.Token == token);

        if (invitation is null || invitation.ExpiresAt <= DateTime.UtcNow || invitation.AcceptedAt != null)
        {
            throw new BadRequestException("Invalid or expired invitation token.");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            throw new ForbiddenException("Forbidden.");
        }

        if (string.IsNullOrWhiteSpace(user.Email)
            || !string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Forbidden.");
        }

        var projectExists = await _dbContext.Projects
            .AnyAsync(x => x.Id == invitation.ProjectId);

        if (!projectExists)
        {
            throw new NotFoundException("Project not found.");
        }

        var existingMembership = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == invitation.ProjectId && x.UserId == userId);

        if (existingMembership is not null)
        {
            invitation.AcceptedAt = DateTime.UtcNow;
            invitation.AcceptedBy = userId;
            await _dbContext.SaveChangesAsync();
            return;
        }

        var now = DateTime.UtcNow;
        var membership = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = invitation.ProjectId,
            UserId = userId,
            Role = invitation.Role,
            JoinedAt = now
        };

        _dbContext.ProjectMembers.Add(membership);
        invitation.AcceptedAt = now;
        invitation.AcceptedBy = userId;

        var project = await _dbContext.Projects.FirstOrDefaultAsync(x => x.Id == invitation.ProjectId);
        if (project is not null)
        {
            project.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> IsValidTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var now = DateTime.UtcNow;

        return await _dbContext.Invitations
            .AnyAsync(x => x.Token == token && x.ExpiresAt > now && x.AcceptedAt == null);
    }

    public async Task<Invitation> GetByTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new NotFoundException("Invitation not found.");
        }

        var invitation = await _dbContext.Invitations
            .Include(x => x.Project)
            .Include(x => x.Inviter)
            .Include(x => x.Accepter)
            .FirstOrDefaultAsync(x => x.Token == token);

        if (invitation is null)
        {
            throw new NotFoundException("Invitation not found.");
        }

        return invitation;
    }

    private static string GenerateSecureToken()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static bool HasRequiredRole(ProjectRole actualRole, ProjectRole minimumRole)
    {
        return actualRole <= minimumRole;
    }
}
