using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kanban.Api.Configuration.Options;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kanban.Api.Services.Auth;

public sealed class TokenService : ITokenService
{
    private const string RefreshTokenCookieName = "refreshToken";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAccessTokenBlocklist _accessTokenBlocklist;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<TokenService> _logger;
    private readonly bool _isDevelopment;

    public TokenService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IAccessTokenBlocklist accessTokenBlocklist,
        IWebHostEnvironment hostEnvironment,
        IOptions<JwtOptions> jwtOptions,
        ILogger<TokenService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _accessTokenBlocklist = accessTokenBlocklist;
        _logger = logger;
        _isDevelopment = hostEnvironment.IsDevelopment();

        var jwt = jwtOptions.Value;
        _jwtSettings = new JwtSettings(
            jwt.Issuer,
            jwt.Audience,
            jwt.Key,
            TimeSpan.FromMinutes(jwt.AccessTokenMinutes),
            TimeSpan.FromDays(jwt.RefreshTokenDays));
    }

    public async Task<AuthResult> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken = default)
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
            TokenHash = HashToken(refreshToken),
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
            user.UserName,
            accessToken,
            accessTokenExpiresAt,
            refreshToken,
            refreshTokenExpiresAt);
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is missing.");
        }

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(refreshToken);

        // Atomic compare-and-revoke. The conditional UPDATE serialises concurrent rotations
        // at the row level: only one request observes a row affected, the others get 0.
        var rowsRevoked = await _dbContext.RefreshTokens
            .Where(x => x.TokenHash == tokenHash && !x.IsRevoked && x.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRevoked, true), cancellationToken);

        if (rowsRevoked == 0)
        {
            _logger.LogWarning("Refresh rejected: token missing, already revoked, or expired.");
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        var userId = await _dbContext.RefreshTokens
            .AsNoTracking()
            .Where(x => x.TokenHash == tokenHash)
            .Select(x => (Guid?)x.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (userId is null)
        {
            _logger.LogWarning("Refresh rejected: revoked token row not found after update.");
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            _logger.LogWarning("Refresh rejected: user {UserId} not found for token.", userId);
            throw new UnauthorizedAccessException("Invalid refresh token user.");
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task LogoutAsync(string? refreshToken = null, CancellationToken cancellationToken = default)
    {
        var effectiveRefreshToken = string.IsNullOrWhiteSpace(refreshToken)
            ? _httpContextAccessor.HttpContext?.Request.Cookies[RefreshTokenCookieName]
            : refreshToken;

        if (!string.IsNullOrWhiteSpace(effectiveRefreshToken))
        {
            var tokenHash = HashToken(effectiveRefreshToken);
            await _dbContext.RefreshTokens
                .Where(x => x.TokenHash == tokenHash)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var accessToken = BearerTokenReader.ReadAccessToken(_httpContextAccessor.HttpContext);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            _accessTokenBlocklist.Block(accessToken);
        }

        DeleteRefreshTokenCookie();
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userRefreshTokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        if (userRefreshTokens.Count > 0)
        {
            _dbContext.RefreshTokens.RemoveRange(userRefreshTokens);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var accessToken = BearerTokenReader.ReadAccessToken(_httpContextAccessor.HttpContext);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            _accessTokenBlocklist.Block(accessToken);
        }

        DeleteRefreshTokenCookie();
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

    public static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return WebEncoders.Base64UrlEncode(hash);
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
                Secure = !_isDevelopment,
                SameSite = _isDevelopment ? SameSiteMode.Lax : SameSiteMode.None,
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
                Secure = !_isDevelopment,
                SameSite = _isDevelopment ? SameSiteMode.Lax : SameSiteMode.None,
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
