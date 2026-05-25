using System.Security.Cryptography;
using System.Text;
using Kanban.Api.Configuration.Options;
using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kanban.Api.Services.Auth;

public sealed class AccountService : IAccountService
{
    private const string AppTokenLoginProvider = "Kanban.Api";
    private const string PasswordResetTokenName = "PasswordResetToken";
    private const string PasswordResetTokenExpiresAtName = "PasswordResetTokenExpiresAt";
    private const string PasswordResetPurpose = "ResetPassword";
    private const string PasswordResetGenericMessage = "If an account with that email exists, a password reset link has been sent.";
    private const string EmailConfirmationGenericMessage = "If an account with that email exists and is not yet confirmed, a new confirmation link has been sent.";
    private const string RegistrationSuccessMessage = "Account created. Check your email for a confirmation link to activate your account.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IAuthProfileService _profileService;
    private readonly ILogger<AccountService> _logger;
    private readonly string _passwordResetUrlBase;
    private readonly string _emailConfirmationUrlBase;

    public AccountService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ITokenService tokenService,
        IEmailService emailService,
        IAuthProfileService profileService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<AccountService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _tokenService = tokenService;
        _emailService = emailService;
        _profileService = profileService;
        _logger = logger;

        var frontend = frontendOptions.Value;
        _passwordResetUrlBase = frontend.ResolvedPasswordResetUrl;
        _emailConfirmationUrlBase = frontend.ResolvedEmailConfirmationUrl;
    }

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim();

        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            throw new ConflictException("A user with this email already exists.", "DUPLICATE_EMAIL");
        }

        var now = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            CreatedAt = now,
            UpdatedAt = now
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errorMessage = string.Join("; ", createResult.Errors.Select(x => x.Description));
            throw new BadRequestException(errorMessage);
        }

        await SendEmailConfirmationAsync(user, cancellationToken);

        return new RegisterResult(RegistrationSuccessMessage, normalizedEmail);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            _logger.LogWarning("Failed login: no user for email {Email}.", normalizedEmail);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Failed login: bad password for user {UserId}.", user.Id);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (!await _userManager.IsEmailConfirmedAsync(user))
        {
            _logger.LogInformation("Login blocked: email not confirmed for user {UserId}.", user.Id);
            throw new UnauthorizedAccessException("Please confirm your email before signing in.");
        }

        _logger.LogInformation("User logged in: {UserId}.", user.Id);
        return await _tokenService.IssueTokensAsync(user, cancellationToken);
    }

    public async Task<ConfirmEmailResult> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(request.Token))
        {
            throw new BadRequestException("Invalid or expired confirmation link.");
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            throw new BadRequestException("Invalid or expired confirmation link.");
        }

        if (await _userManager.IsEmailConfirmedAsync(user))
        {
            return new ConfirmEmailResult("Email is already confirmed. You can sign in.");
        }

        var decodedToken = DecodeBase64UrlOrUseRaw(request.Token);
        var confirmResult = await _userManager.ConfirmEmailAsync(user, decodedToken);
        if (!confirmResult.Succeeded)
        {
            throw new BadRequestException("Invalid or expired confirmation link.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ConfirmEmailResult("Email confirmed. You can now sign in.");
    }

    public async Task<ResendConfirmationResult> ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new ResendConfirmationResult(EmailConfirmationGenericMessage);
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null || await _userManager.IsEmailConfirmedAsync(user))
        {
            return new ResendConfirmationResult(EmailConfirmationGenericMessage);
        }

        await SendEmailConfirmationAsync(user, cancellationToken);

        return new ResendConfirmationResult(EmailConfirmationGenericMessage);
    }

    public async Task<PasswordResetRequestResult> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new PasswordResetRequestResult(PasswordResetGenericMessage);
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            return new PasswordResetRequestResult(PasswordResetGenericMessage);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var expiresAt = DateTime.UtcNow.AddHours(1);

        await _userManager.SetAuthenticationTokenAsync(user, AppTokenLoginProvider, PasswordResetTokenName, token);
        await _userManager.SetAuthenticationTokenAsync(user, AppTokenLoginProvider, PasswordResetTokenExpiresAtName, expiresAt.ToString("O"));

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var tokenBytes = Encoding.UTF8.GetBytes(token);
            var encodedToken = WebEncoders.Base64UrlEncode(tokenBytes);
            var encodedEmail = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(user.Email));
            var resetUrl = $"{_passwordResetUrlBase}?token={Uri.EscapeDataString(encodedToken)}&email={Uri.EscapeDataString(encodedEmail)}";

            var body = EmailTemplates.PasswordReset(resetUrl, "1 hour");
            await _emailService.SendAsync(user.Email, "Reset your Kanban Ripple password", body, cancellationToken);
        }

        return new PasswordResetRequestResult(PasswordResetGenericMessage);
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(request.Token))
        {
            throw new BadRequestException("Invalid or expired password reset token.");
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            throw new BadRequestException("Invalid or expired password reset token.");
        }

        var storedToken = await _userManager.GetAuthenticationTokenAsync(user, AppTokenLoginProvider, PasswordResetTokenName);
        var storedExpiresAt = await _userManager.GetAuthenticationTokenAsync(user, AppTokenLoginProvider, PasswordResetTokenExpiresAtName);

        if (string.IsNullOrWhiteSpace(storedToken)
            || string.IsNullOrWhiteSpace(storedExpiresAt)
            || !DateTime.TryParse(storedExpiresAt, out var expiresAt)
            || expiresAt <= DateTime.UtcNow)
        {
            throw new BadRequestException("Invalid or expired password reset token.");
        }

        var incomingToken = DecodeBase64UrlOrUseRaw(request.Token);
        if (!FixedTimeEquals(incomingToken, storedToken))
        {
            throw new BadRequestException("Invalid or expired password reset token.");
        }

        var isIdentityTokenValid = await _userManager.VerifyUserTokenAsync(
            user,
            _userManager.Options.Tokens.PasswordResetTokenProvider,
            PasswordResetPurpose,
            incomingToken);

        if (!isIdentityTokenValid)
        {
            throw new BadRequestException("Invalid or expired password reset token.");
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, incomingToken, request.NewPassword);
        if (!resetResult.Succeeded)
        {
            var errorMessage = string.Join("; ", resetResult.Errors.Select(x => x.Description));
            throw new BadRequestException(errorMessage);
        }

        await _userManager.RemoveAuthenticationTokenAsync(user, AppTokenLoginProvider, PasswordResetTokenName);
        await _userManager.RemoveAuthenticationTokenAsync(user, AppTokenLoginProvider, PasswordResetTokenExpiresAtName);

        return new PasswordResetResult("Password has been reset successfully.");
    }

    public async Task<AccountDeletionEligibilityResult> CanDeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var ownedProjectCount = await _dbContext.Projects
            .IgnoreQueryFilters()
            .CountAsync(x => x.OwnerId == userId, cancellationToken);

        if (ownedProjectCount > 0)
        {
            return new AccountDeletionEligibilityResult(
                CanDelete: false,
                OwnedProjectCount: ownedProjectCount,
                Reason: "Transfer ownership of all owned projects before deleting your account.");
        }

        return new AccountDeletionEligibilityResult(
            CanDelete: true,
            OwnedProjectCount: 0,
            Reason: null);
    }

    public async Task DeleteAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var eligibility = await CanDeleteAccountAsync(userId, cancellationToken);
        if (!eligibility.CanDelete)
        {
            throw new BadRequestException(eligibility.Reason ?? "Account cannot be deleted.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        await _tokenService.RevokeAllForUserAsync(userId, cancellationToken);

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            var errorMessage = string.Join("; ", deleteResult.Errors.Select(x => x.Description));
            throw new BadRequestException(errorMessage);
        }
    }

    public Task UploadProfilePhotoAsync(Guid userId, IFormFile file, CancellationToken cancellationToken = default) =>
        _profileService.UploadProfilePhotoAsync(userId, file, cancellationToken);

    public Task<(Stream Content, string ContentType)?> GetProfilePhotoStreamAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _profileService.GetProfilePhotoStreamAsync(userId, cancellationToken);

    public Task DeleteProfilePhotoAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _profileService.DeleteProfilePhotoAsync(userId, cancellationToken);

    public async Task<ApplicationUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _userManager.FindByIdAsync(userId.ToString());
    }

    public Task<ChangePasswordResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default) =>
        _profileService.ChangePasswordAsync(userId, request, cancellationToken);

    public Task<UpdateDisplayNameResult> UpdateDisplayNameAsync(Guid userId, UpdateDisplayNameRequest request, CancellationToken cancellationToken = default) =>
        _profileService.UpdateDisplayNameAsync(userId, request, cancellationToken);

    private async Task SendEmailConfirmationAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var encodedEmail = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(user.Email));
        var confirmUrl = $"{_emailConfirmationUrlBase}?token={Uri.EscapeDataString(encodedToken)}&email={Uri.EscapeDataString(encodedEmail)}";

        var body = EmailTemplates.EmailConfirmation(confirmUrl);
        await _emailService.SendAsync(user.Email, "Confirm your Kanban Ripple email", body, cancellationToken);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string DecodeBase64UrlOrUseRaw(string value)
    {
        try
        {
            var decoded = WebEncoders.Base64UrlDecode(value);
            return Encoding.UTF8.GetString(decoded);
        }
        catch (FormatException)
        {
            return value;
        }
    }
}
