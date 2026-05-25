using Kanban.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

public sealed class AuthController : KanbanControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";

    private readonly IAccountService _accountService;
    private readonly ITokenService _tokenService;

    public AuthController(IAccountService accountService, ITokenService tokenService)
    {
        _accountService = accountService;
        _tokenService = tokenService;
    }

    [HttpPost("auth/register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterResult>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _accountService.RegisterAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("auth/confirm-email")]
    [AllowAnonymous]
    public async Task<ActionResult<ConfirmEmailResult>> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _accountService.ConfirmEmailAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("auth/resend-confirmation")]
    [AllowAnonymous]
    public async Task<ActionResult<ResendConfirmationResult>> ResendConfirmation(
        [FromBody] ResendConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _accountService.ResendConfirmationAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _accountService.LoginAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("auth/logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _tokenService.LogoutAsync(cancellationToken: cancellationToken);
        return NoContent();
    }

    [HttpPost("auth/refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new { message = "Refresh token is missing." });
        }

        var result = await _tokenService.RefreshTokenAsync(refreshToken, cancellationToken);
        return Ok(result);
    }

    [HttpPost("auth/password-reset")]
    [AllowAnonymous]
    public async Task<ActionResult<PasswordResetRequestResult>> RequestPasswordReset(
        [FromBody] PasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _accountService.RequestPasswordResetAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("auth/password-reset")]
    [AllowAnonymous]
    public async Task<ActionResult<PasswordResetResult>> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _accountService.ResetPasswordAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("auth/me")]
    [Authorize]
    public async Task<ActionResult<object>> Me(CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var user = await _accountService.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { message = "User not found." });
        }

        return Ok(new { userId, email = user.Email ?? string.Empty, userName = user.UserName });
    }

    [HttpGet("auth/users/{userId:guid}/profile-photo")]
    [Authorize]
    public async Task<IActionResult> GetUserProfilePhoto(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _accountService.GetProfilePhotoStreamAsync(userId, cancellationToken);
        if (result is null)
        {
            return NoContent();
        }

        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpPost("auth/profile-photo")]
    [Authorize]
    public async Task<IActionResult> UploadProfilePhoto(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await _accountService.UploadProfilePhotoAsync(userId, file, cancellationToken);
        return Ok(new { message = "Profile photo uploaded." });
    }

    [HttpGet("auth/profile-photo")]
    [Authorize]
    public async Task<IActionResult> GetProfilePhoto(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _accountService.GetProfilePhotoStreamAsync(userId, cancellationToken);
        if (result is null)
        {
            return NoContent();
        }

        return File(result.Value.Content, result.Value.ContentType);
    }

    [HttpDelete("auth/profile-photo")]
    [Authorize]
    public async Task<IActionResult> DeleteProfilePhoto(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await _accountService.DeleteProfilePhotoAsync(userId, cancellationToken);
        return NoContent();
    }

    [HttpPut("auth/password")]
    [Authorize]
    public async Task<ActionResult<ChangePasswordResult>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _accountService.ChangePasswordAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("auth/display-name")]
    [Authorize]
    public async Task<ActionResult<UpdateDisplayNameResult>> UpdateDisplayName(
        [FromBody] UpdateDisplayNameRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _accountService.UpdateDisplayNameAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("auth/account")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        var eligibility = await _accountService.CanDeleteAccountAsync(userId, cancellationToken);
        if (!eligibility.CanDelete)
        {
            return Conflict(eligibility);
        }

        await _accountService.DeleteAccountAsync(userId, cancellationToken);
        return NoContent();
    }
}
