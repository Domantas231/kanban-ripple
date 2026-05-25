using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Kanban.Api.Services.Auth;

internal static class BearerTokenReader
{
    private const string HubsPathPrefix = "/hubs";
    private const string AccessTokenQueryParam = "access_token";

    public static string? ReadAccessToken(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        var headerToken = ReadFromAuthorizationHeader(httpContext.Request);
        if (!string.IsNullOrWhiteSpace(headerToken))
        {
            return headerToken;
        }

        // SignalR/WebSocket clients cannot set Authorization headers, so the JWT bearer
        // configuration accepts the token via ?access_token= for /hubs requests. Mirror
        // that here so the access-token blocklist sees the same token JwtBearer validated.
        if (httpContext.Request.Path.StartsWithSegments(HubsPathPrefix))
        {
            var queryToken = httpContext.Request.Query[AccessTokenQueryParam].ToString();
            return string.IsNullOrWhiteSpace(queryToken) ? null : queryToken;
        }

        return null;
    }

    private static string? ReadFromAuthorizationHeader(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(HeaderNames.Authorization, out var authorizationValue))
        {
            return null;
        }

        var rawValue = authorizationValue.ToString();
        const string bearerPrefix = "Bearer ";
        if (!rawValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = rawValue[bearerPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
