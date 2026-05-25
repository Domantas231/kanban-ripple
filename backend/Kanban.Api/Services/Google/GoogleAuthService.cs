using System.Net.Http.Headers;
using System.Text.Json;
using Kanban.Api.Configuration.Options;
using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kanban.Api.Services.Google;

public sealed class GoogleAuthService : IGoogleAuthService
{
    private const string ProtectorPurpose = "GoogleOAuthTokens";
    private const string GoogleAuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string GoogleUserInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";
    private const string GoogleRevokeEndpoint = "https://oauth2.googleapis.com/revoke";
    private const string Scopes = "https://www.googleapis.com/auth/drive https://www.googleapis.com/auth/userinfo.email https://www.googleapis.com/auth/calendar.events";
    private readonly ApplicationDbContext _dbContext;
    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGoogleDriveApiClient _googleDriveApiClient;
    private readonly IProjectBroadcaster _projectBroadcaster;
    private readonly ILogger<GoogleAuthService> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;

    public GoogleAuthService(
        ApplicationDbContext dbContext,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IGoogleDriveApiClient googleDriveApiClient,
        IProjectBroadcaster projectBroadcaster,
        IOptions<GoogleOAuthOptions> options,
        ILogger<GoogleAuthService> logger)
    {
        _dbContext = dbContext;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _httpClientFactory = httpClientFactory;
        _googleDriveApiClient = googleDriveApiClient;
        _projectBroadcaster = projectBroadcaster;
        _logger = logger;

        var googleOptions = options.Value;
        if (string.IsNullOrWhiteSpace(googleOptions.ClientId))
        {
            throw new InvalidOperationException("Google:ClientId is missing.");
        }
        if (string.IsNullOrWhiteSpace(googleOptions.ClientSecret))
        {
            throw new InvalidOperationException("Google:ClientSecret is missing.");
        }
        if (string.IsNullOrWhiteSpace(googleOptions.RedirectUri))
        {
            throw new InvalidOperationException("Google:RedirectUri is missing.");
        }

        _clientId = googleOptions.ClientId;
        _clientSecret = googleOptions.ClientSecret;
        _redirectUri = googleOptions.RedirectUri;
    }

    public string BuildAuthUrl(Guid userId)
    {
        var state = _protector.Protect(userId.ToString());

        var queryParams = new Dictionary<string, string?>
        {
            ["client_id"] = _clientId,
            ["redirect_uri"] = _redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scopes,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state
        };

        var query = string.Join("&", queryParams
            .Where(kvp => kvp.Value is not null)
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));

        return $"{GoogleAuthEndpoint}?{query}";
    }

    public async Task ExchangeCodeAsync(string code, Guid userId, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = _redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var tokenResponse = await client.PostAsync(GoogleTokenEndpoint, tokenRequest, cancellationToken);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new BadRequestException($"Failed to exchange authorization code: {tokenJson}");
        }

        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
        var accessToken = tokenData.GetProperty("access_token").GetString()
            ?? throw new BadRequestException("Missing access_token in token response.");
        var refreshToken = tokenData.GetProperty("refresh_token").GetString()
            ?? throw new BadRequestException("Missing refresh_token in token response.");
        var expiresIn = tokenData.GetProperty("expires_in").GetInt32();

        using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, GoogleUserInfoEndpoint);
        userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var userInfoResponse = await client.SendAsync(userInfoRequest, cancellationToken);
        var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!userInfoResponse.IsSuccessStatusCode)
        {
            throw new BadRequestException($"Failed to fetch Google user info: {userInfoJson}");
        }

        var userInfo = JsonSerializer.Deserialize<JsonElement>(userInfoJson);
        var googleEmail = userInfo.GetProperty("email").GetString()
            ?? throw new BadRequestException("Missing email in user info response.");
        var googleUserId = userInfo.GetProperty("id").GetString()
            ?? throw new BadRequestException("Missing id in user info response.");

        var encryptedAccessToken = _protector.Protect(accessToken);
        var encryptedRefreshToken = _protector.Protect(refreshToken);
        var tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

        var existing = await _dbContext.UserGoogleAccounts
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            existing.GoogleEmail = googleEmail;
            existing.GoogleUserId = googleUserId;
            existing.EncryptedAccessToken = encryptedAccessToken;
            existing.EncryptedRefreshToken = encryptedRefreshToken;
            existing.TokenExpiresAt = tokenExpiresAt;
        }
        else
        {
            var account = new UserGoogleAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GoogleEmail = googleEmail,
                GoogleUserId = googleUserId,
                EncryptedAccessToken = encryptedAccessToken,
                EncryptedRefreshToken = encryptedRefreshToken,
                TokenExpiresAt = tokenExpiresAt,
                ConnectedAt = DateTime.UtcNow
            };
            _dbContext.UserGoogleAccounts.Add(account);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await BackfillDriveSharesAsync(userId, googleEmail, cancellationToken);
    }

    private async Task BackfillDriveSharesAsync(Guid userId, string googleEmail, CancellationToken cancellationToken)
    {
        var links = await _dbContext.GoogleDriveLinks
            .Include(l => l.Card)
                .ThenInclude(c => c.Column)
                    .ThenInclude(col => col.Board)
            .Where(l => l.DeletedAt == null
                && l.LinkedBy != userId
                && _dbContext.ProjectMembers.Any(pm =>
                    pm.UserId == userId
                    && pm.ProjectId == l.Card.Column.Board.ProjectId))
            .ToListAsync(cancellationToken);

        if (links.Count == 0)
        {
            return;
        }

        foreach (var linkerGroup in links.GroupBy(l => l.LinkedBy))
        {
            string linkerAccessToken;
            try
            {
                linkerAccessToken = await GetAccessTokenAsync(linkerGroup.Key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not obtain access token for linker {LinkerId} during Drive backfill for newly connected user {UserId}.", linkerGroup.Key, userId);
                continue;
            }

            foreach (var link in linkerGroup)
            {
                try
                {
                    var role = link.SharePermission.ToString().ToLowerInvariant();
                    await _googleDriveApiClient.AddPermissionAsync(linkerAccessToken, link.GoogleFileId, googleEmail, role, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to backfill Drive permission for file {FileId} to user {UserId}.", link.GoogleFileId, userId);
                }
            }
        }
    }

    public async Task<string> GetAccessTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.UserGoogleAccounts
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Google account not connected.");

        if (account.TokenExpiresAt > DateTime.UtcNow.AddSeconds(60))
        {
            return _protector.Unprotect(account.EncryptedAccessToken);
        }

        var refreshToken = _protector.Unprotect(account.EncryptedRefreshToken);
        using var client = _httpClientFactory.CreateClient();

        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        });

        var tokenResponse = await client.PostAsync(GoogleTokenEndpoint, tokenRequest, cancellationToken);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new BadRequestException($"Failed to refresh Google access token: {tokenJson}");
        }

        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
        var newAccessToken = tokenData.GetProperty("access_token").GetString()
            ?? throw new BadRequestException("Missing access_token in refresh response.");
        var expiresIn = tokenData.GetProperty("expires_in").GetInt32();

        account.EncryptedAccessToken = _protector.Protect(newAccessToken);
        account.TokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

        // Google may issue a new refresh token
        if (tokenData.TryGetProperty("refresh_token", out var newRefreshTokenElement))
        {
            var newRefreshToken = newRefreshTokenElement.GetString();
            if (!string.IsNullOrEmpty(newRefreshToken))
            {
                account.EncryptedRefreshToken = _protector.Protect(newRefreshToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return newAccessToken;
    }

    public async Task<GoogleConnectionStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.UserGoogleAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (account is null)
        {
            return new GoogleConnectionStatusDto { Connected = false };
        }

        return new GoogleConnectionStatusDto
        {
            Connected = true,
            GoogleEmail = account.GoogleEmail,
            ConnectedAt = account.ConnectedAt
        };
    }

    public async Task DisconnectAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.UserGoogleAccounts
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Google account not connected.");

        // Obtain a valid (refreshed if needed) access token for Drive cleanup.
        // If the refresh token has been externally revoked we still soft-delete
        // local link rows so the files vanish from cards.
        string? accessToken = null;
        try
        {
            accessToken = await GetAccessTokenAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not obtain Google access token while disconnecting user {UserId}; will skip Drive permission revocation.", userId);
        }

        await CleanupUserDriveLinksAsync(userId, accessToken, cancellationToken);

        if (accessToken is not null)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                var revokeRequest = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = accessToken
                });
                await client.PostAsync(GoogleRevokeEndpoint, revokeRequest, cancellationToken);
            }
            catch
            {
                // Revocation failure should not block disconnect
            }
        }

        _dbContext.UserGoogleAccounts.Remove(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CleanupUserDriveLinksAsync(Guid userId, string? accessToken, CancellationToken cancellationToken)
    {
        var links = await _dbContext.GoogleDriveLinks
            .Include(l => l.Card)
                .ThenInclude(c => c.Column)
                    .ThenInclude(col => col.Board)
            .Where(l => l.LinkedBy == userId && l.DeletedAt == null)
            .ToListAsync(cancellationToken);

        if (links.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var linksByProject = links
            .GroupBy(l => l.Card.Column.Board.ProjectId)
            .ToList();

        foreach (var projectGroup in linksByProject)
        {
            HashSet<string>? memberEmailSet = null;

            if (accessToken is not null)
            {
                var memberEmails = await _dbContext.ProjectMembers
                    .Where(pm => pm.ProjectId == projectGroup.Key && pm.UserId != userId)
                    .Join(
                        _dbContext.UserGoogleAccounts,
                        pm => pm.UserId,
                        ga => ga.UserId,
                        (pm, ga) => ga.GoogleEmail)
                    .ToListAsync(cancellationToken);

                memberEmailSet = new HashSet<string>(memberEmails, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var link in projectGroup)
            {
                if (accessToken is not null && memberEmailSet is { Count: > 0 })
                {
                    await TryRevokeFileSharesAsync(accessToken, link, memberEmailSet, cancellationToken);
                }

                link.DeletedAt = now;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var link in links)
        {
            try
            {
                await _projectBroadcaster.CardUpdated(link.Card.Column.Board.ProjectId, link.Card);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast CardUpdated for card {CardId} during Google disconnect cleanup.", link.CardId);
            }
        }
    }

    private async Task TryRevokeFileSharesAsync(
        string accessToken,
        GoogleDriveLink link,
        HashSet<string> memberEmailSet,
        CancellationToken cancellationToken)
    {
        List<GoogleFilePermission> permissions;
        try
        {
            permissions = await _googleDriveApiClient.ListPermissionsAsync(accessToken, link.GoogleFileId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list Drive permissions for file {FileId} during disconnect cleanup.", link.GoogleFileId);
            return;
        }

        foreach (var permission in permissions)
        {
            if (permission.EmailAddress is null
                || string.Equals(permission.Role, "owner", StringComparison.OrdinalIgnoreCase)
                || !memberEmailSet.Contains(permission.EmailAddress))
            {
                continue;
            }

            try
            {
                await _googleDriveApiClient.DeletePermissionAsync(accessToken, link.GoogleFileId, permission.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to revoke Drive permission {PermissionId} on file {FileId} during disconnect cleanup.", permission.Id, link.GoogleFileId);
            }
        }
    }

    public Guid? ValidateState(string state)
    {
        try
        {
            var decrypted = _protector.Unprotect(state);
            if (Guid.TryParse(decrypted, out var userId))
            {
                return userId;
            }
        }
        catch
        {
            // Invalid or tampered state
        }

        return null;
    }
}
