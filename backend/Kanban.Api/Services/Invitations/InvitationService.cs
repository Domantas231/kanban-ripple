using System.Security.Cryptography;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Invitations;

public sealed class InvitationService : IInvitationService
{
    private const int InvitationLifetimeDays = 7;
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly string _acceptInvitationUrlBase;

    public InvitationService(ApplicationDbContext dbContext, IEmailService emailService, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _emailService = emailService;

        var frontendUrl = configuration["Frontend:Url"] ?? "http://localhost:5173";
        _acceptInvitationUrlBase = configuration["Frontend:InvitationAcceptUrl"]?.TrimEnd('/')
            ?? $"{frontendUrl.TrimEnd('/')}/invitations/accept";
    }

    public async Task<string> CreateInvitationAsync(Guid projectId, Guid invitedBy, string email)
    {
        var normalizedEmail = email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new InvalidOperationException("Invitation email is required.");
        }

        var membership = await _dbContext.ProjectMembers
            .Where(x => x.ProjectId == projectId && x.UserId == invitedBy)
            .Select(x => (ProjectRole?)x.Role)
            .FirstOrDefaultAsync();

        if (membership is null || !HasRequiredRole(membership.Value, ProjectRole.Moderator))
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var projectExists = await _dbContext.Projects.AnyAsync(x => x.Id == projectId);
        if (!projectExists)
        {
            throw new KeyNotFoundException("Project not found.");
        }

        var existingMemberByEmail = await _dbContext.ProjectMembers
            .Where(x => x.ProjectId == projectId)
            .Include(x => x.User)
            .AnyAsync(x => x.User.Email != null && x.User.Email.ToLower() == normalizedEmail.ToLower());

        if (existingMemberByEmail)
        {
            throw new InvalidOperationException("User is already a project member.");
        }

        var token = GenerateSecureToken();
        var now = DateTime.UtcNow;

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Email = normalizedEmail,
            Token = token,
            InvitedBy = invitedBy,
            CreatedAt = now,
            ExpiresAt = now.AddDays(InvitationLifetimeDays)
        };

        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        var invitationUrl = $"{_acceptInvitationUrlBase}?token={Uri.EscapeDataString(token)}";
        var body = $"You have been invited to join a project. This invitation expires in {InvitationLifetimeDays} days.\n\n{invitationUrl}";
        await _emailService.SendAsync(normalizedEmail, "Project invitation", body);

        return token;
    }

    public async Task AcceptInvitationAsync(string token, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Invalid or expired invitation token.");
        }

        var invitation = await _dbContext.Invitations
            .FirstOrDefaultAsync(x => x.Token == token);

        if (invitation is null || invitation.ExpiresAt <= DateTime.UtcNow || invitation.AcceptedAt != null)
        {
            throw new InvalidOperationException("Invalid or expired invitation token.");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        if (string.IsNullOrWhiteSpace(user.Email)
            || !string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var projectExists = await _dbContext.Projects
            .AnyAsync(x => x.Id == invitation.ProjectId);

        if (!projectExists)
        {
            throw new KeyNotFoundException("Project not found.");
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
            Role = ProjectRole.Member,
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
            throw new KeyNotFoundException("Invitation not found.");
        }

        var invitation = await _dbContext.Invitations
            .Include(x => x.Project)
            .Include(x => x.Inviter)
            .Include(x => x.Accepter)
            .FirstOrDefaultAsync(x => x.Token == token);

        if (invitation is null)
        {
            throw new KeyNotFoundException("Invitation not found.");
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
