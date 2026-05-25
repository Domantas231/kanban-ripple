using System.Text;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Controllers;

// Test-only endpoints used by Playwright E2E setup.
// Registered only when ASPNETCORE_ENVIRONMENT=Development (see ServiceFilter below).
[ApiController]
[Route("api/test")]
[AllowAnonymous]
public sealed class TestController : ControllerBase
{
    private const string AppTokenLoginProvider = "Kanban.Api";
    private const string PasswordResetTokenName = "PasswordResetToken";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public TestController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _environment = environment;
    }

    public sealed record ConfirmEmailTestRequest(string Email);

    public sealed record DeleteUserTestRequest(string Email);

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailTestRequest request,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (user.EmailConfirmed)
        {
            return Ok(new { message = "Already confirmed." });
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var result = await _userManager.ConfirmEmailAsync(user, token);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = "Confirmation failed.", errors = result.Errors });
        }

        return Ok(new { message = "Confirmed." });
    }

    [HttpPost("delete-user")]
    public async Task<IActionResult> DeleteUser(
        [FromBody] DeleteUserTestRequest request,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Ok(new { message = "User did not exist." });
        }

        // Cascade delete owned data so we can re-provision the same email cleanly.
        var ownedProjectIds = await _dbContext.Projects
            .Where(p => p.OwnerId == user.Id)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (ownedProjectIds.Count > 0)
        {
            await _dbContext.Projects
                .Where(p => ownedProjectIds.Contains(p.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = "User delete failed.", errors = result.Errors });
        }

        return Ok(new { message = "Deleted." });
    }

    /// <summary>
    /// Returns the active password-reset token for a user, base64url-encoded
    /// and ready to be passed back as the <c>token</c> query-string value.
    /// Used by E2E tests that need to drive the reset flow without parsing
    /// the email body. Only available in Development.
    /// </summary>
    [HttpGet("password-reset-token")]
    public async Task<IActionResult> GetPasswordResetToken(
        [FromQuery] string email,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var rawToken = await _userManager.GetAuthenticationTokenAsync(
            user,
            AppTokenLoginProvider,
            PasswordResetTokenName);

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return NotFound(new { message = "No active password-reset token. Call POST /api/auth/password-reset first." });
        }

        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
        var encodedEmail = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(user.Email!));

        return Ok(new { encodedToken, encodedEmail });
    }

    /// <summary>
    /// Returns the most recent unaccepted invitation token sent to the given
    /// email for the given project. Used by E2E tests that need the token to
    /// drive the /invitations/accept route. Only available in Development.
    /// </summary>
    [HttpGet("invitation-token")]
    public async Task<IActionResult> GetInvitationToken(
        [FromQuery] Guid projectId,
        [FromQuery] string email,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var invitation = await _dbContext.Invitations
            .Where(i => i.ProjectId == projectId
                        && i.Email == email
                        && i.AcceptedAt == null)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new { i.Token, i.ExpiresAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (invitation is null)
        {
            return NotFound(new { message = "No active invitation found for that project + email." });
        }

        return Ok(new { token = invitation.Token, expiresAt = invitation.ExpiresAt });
    }
}
