using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Caching.Memory;

namespace Kanban.Api.Services.Auth;

public sealed class MemoryAccessTokenBlocklist : IAccessTokenBlocklist
{
    private const string CacheKeyPrefix = "blocked-access-token:";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _memoryCache;

    public MemoryAccessTokenBlocklist(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public void Block(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        var ttl = ResolveTtl(accessToken);
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        _memoryCache.Set(BuildCacheKey(accessToken), true, ttl);
    }

    public bool IsBlocked(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        return _memoryCache.TryGetValue(BuildCacheKey(accessToken), out _);
    }

    private static string BuildCacheKey(string accessToken) => $"{CacheKeyPrefix}{accessToken}";

    private static TimeSpan ResolveTtl(string accessToken)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(accessToken))
        {
            return DefaultTtl;
        }

        var jwt = handler.ReadJwtToken(accessToken);
        var expClaim = jwt.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Exp)?.Value;
        if (!long.TryParse(expClaim, out var expUnixSeconds))
        {
            return DefaultTtl;
        }

        var expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expUnixSeconds);
        var ttl = expiresAtUtc - DateTimeOffset.UtcNow;
        return ttl > TimeSpan.Zero ? ttl : TimeSpan.Zero;
    }
}
