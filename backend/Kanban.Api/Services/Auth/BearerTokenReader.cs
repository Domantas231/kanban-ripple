using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Kanban.Api.Services.Auth;

internal static class BearerTokenReader
{
    public static string? ReadAccessToken(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        if (!httpContext.Request.Headers.TryGetValue(HeaderNames.Authorization, out var authorizationValue))
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
