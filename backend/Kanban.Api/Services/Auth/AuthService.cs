using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Kanban.Api.Services.Auth;

public class AuthService : IAuthService
{
    private const string RefreshTokenCookieName = "refreshToken";
    private const string AppTokenLoginProvider = "Kanban.Api";
    private const string PasswordResetTokenName = "PasswordResetToken";
    private const string PasswordResetTokenExpiresAtName = "PasswordResetTokenExpiresAt";
    private const string PasswordResetPurpose = "ResetPassword";
    private const string PasswordResetGenericMessage = "If an account with that email exists, a password reset link has been sent.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAccessTokenBlocklist _accessTokenBlocklist;
    private readonly JwtSettings _jwtSettings;
    private readonly IEmailService? _emailService;
    private readonly string _passwordResetUrlBase;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IAccessTokenBlocklist accessTokenBlocklist,
        IConfiguration configuration,
        IEmailService? emailService = null)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _accessTokenBlocklist = accessTokenBlocklist;
        _emailService = emailService;

        var jwtSection = configuration.GetSection("Jwt");
        var issuer = jwtSection["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is missing.");
        var audience = jwtSection["Audience"] ?? throw new InvalidOperationException("Jwt:Audience is missing.");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.");

        var accessTokenMinutes = jwtSection.GetValue<int?>("AccessTokenMinutes") ?? 15;
        var refreshTokenDays = jwtSection.GetValue<int?>("RefreshTokenDays") ?? 7;

        _jwtSettings = new JwtSettings(
            issuer,
            audience,
            key,
            TimeSpan.FromMinutes(accessTokenMinutes),
            TimeSpan.FromDays(refreshTokenDays));

        var frontendUrl = configuration["Frontend:Url"] ?? "http://localhost:5173";
        _passwordResetUrlBase = configuration["Frontend:PasswordResetUrl"]?.TrimEnd('/')
            ?? $"{frontendUrl.TrimEnd('/')}/reset-password";
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim();

        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            throw new InvalidOperationException("A user with this email already exists.");
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
            throw new InvalidOperationException(errorMessage);
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is missing.");
        }

        var now = DateTime.UtcNow;
        var tokenEntity = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(x => x.Token == refreshToken, cancellationToken);

        if (tokenEntity is null || tokenEntity.IsRevoked || tokenEntity.ExpiresAt <= now)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        var user = await _userManager.FindByIdAsync(tokenEntity.UserId.ToString());
        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token user.");
        }

        tokenEntity.IsRevoked = true;

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task LogoutAsync(string? refreshToken = null, CancellationToken cancellationToken = default)
    {
        var effectiveRefreshToken = string.IsNullOrWhiteSpace(refreshToken)
            ? _httpContextAccessor.HttpContext?.Request.Cookies[RefreshTokenCookieName]
            : refreshToken;

        if (!string.IsNullOrWhiteSpace(effectiveRefreshToken))
        {
            var tokenEntity = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(x => x.Token == effectiveRefreshToken, cancellationToken);

            if (tokenEntity is not null)
            {
                _dbContext.RefreshTokens.Remove(tokenEntity);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var accessToken = BearerTokenReader.ReadAccessToken(_httpContextAccessor.HttpContext);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            _accessTokenBlocklist.Block(accessToken);
        }

        DeleteRefreshTokenCookie();
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

        if (_emailService is not null && !string.IsNullOrWhiteSpace(user.Email))
        {
            var tokenBytes = Encoding.UTF8.GetBytes(token);
            var encodedToken = WebEncoders.Base64UrlEncode(tokenBytes);
            var encodedEmail = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(user.Email));
            var resetUrl = $"{_passwordResetUrlBase}?token={Uri.EscapeDataString(encodedToken)}&email={Uri.EscapeDataString(encodedEmail)}";

            var body = $"Use this link to reset your password. The link expires in 1 hour.\n\n{resetUrl}";
            await _emailService.SendAsync(user.Email, "Reset your password", body, cancellationToken);
        }

        return new PasswordResetRequestResult(PasswordResetGenericMessage);
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(request.Token))
        {
            throw new InvalidOperationException("Invalid or expired password reset token.");
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            throw new InvalidOperationException("Invalid or expired password reset token.");
        }

        var storedToken = await _userManager.GetAuthenticationTokenAsync(user, AppTokenLoginProvider, PasswordResetTokenName);
        var storedExpiresAt = await _userManager.GetAuthenticationTokenAsync(user, AppTokenLoginProvider, PasswordResetTokenExpiresAtName);

        if (string.IsNullOrWhiteSpace(storedToken)
            || string.IsNullOrWhiteSpace(storedExpiresAt)
            || !DateTime.TryParse(storedExpiresAt, out var expiresAt)
            || expiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Invalid or expired password reset token.");
        }

        var incomingToken = DecodeBase64UrlOrUseRaw(request.Token);
        if (!FixedTimeEquals(incomingToken, storedToken))
        {
            throw new InvalidOperationException("Invalid or expired password reset token.");
        }

        var isIdentityTokenValid = await _userManager.VerifyUserTokenAsync(
            user,
            _userManager.Options.Tokens.PasswordResetTokenProvider,
            PasswordResetPurpose,
            incomingToken);

        if (!isIdentityTokenValid)
        {
            throw new InvalidOperationException("Invalid or expired password reset token.");
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, incomingToken, request.NewPassword);
        if (!resetResult.Succeeded)
        {
            var errorMessage = string.Join("; ", resetResult.Errors.Select(x => x.Description));
            throw new InvalidOperationException(errorMessage);
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
            throw new InvalidOperationException(eligibility.Reason);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var userRefreshTokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        if (userRefreshTokens.Count > 0)
        {
            _dbContext.RefreshTokens.RemoveRange(userRefreshTokens);
        }

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            var errorMessage = string.Join("; ", deleteResult.Errors.Select(x => x.Description));
            throw new InvalidOperationException(errorMessage);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = BearerTokenReader.ReadAccessToken(_httpContextAccessor.HttpContext);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            _accessTokenBlocklist.Block(accessToken);
        }

        DeleteRefreshTokenCookie();
    }

    private async Task<AuthResult> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var accessTokenExpiresAt = now.Add(_jwtSettings.AccessTokenLifetime);
        var refreshTokenExpiresAt = now.Add(_jwtSettings.RefreshTokenLifetime);

        var accessToken = GenerateAccessToken(user, accessTokenExpiresAt);
        var refreshToken = GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            CreatedAt = now,
            ExpiresAt = refreshTokenExpiresAt,
            IsRevoked = false
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        SetRefreshTokenCookie(refreshToken, refreshTokenExpiresAt);

        return new AuthResult(
            user.Id,
            user.Email ?? string.Empty,
            accessToken,
            accessTokenExpiresAt,
            refreshToken,
            refreshTokenExpiresAt);
    }

    private string GenerateAccessToken(ApplicationUser user, DateTime expiresAt)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSettings.Key);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return WebEncoders.Base64UrlEncode(bytes);
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

    private void SetRefreshTokenCookie(string refreshToken, DateTime expiresAtUtc)
    {
        var response = _httpContextAccessor.HttpContext?.Response;
        if (response is null)
        {
            return;
        }

        response.Cookies.Append(
            RefreshTokenCookieName,
            refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = expiresAtUtc,
                Path = "/"
            });
    }

    private void DeleteRefreshTokenCookie()
    {
        var response = _httpContextAccessor.HttpContext?.Response;
        if (response is null)
        {
            return;
        }

        response.Cookies.Delete(
            RefreshTokenCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
    }

    private sealed record JwtSettings(
        string Issuer,
        string Audience,
        string Key,
        TimeSpan AccessTokenLifetime,
        TimeSpan RefreshTokenLifetime);
}
