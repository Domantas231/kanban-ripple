using Kanban.Api.Services.Auth;

namespace Kanban.Api.Middleware;

public sealed class AccessTokenBlocklistMiddleware
{
    private readonly RequestDelegate _next;

    public AccessTokenBlocklistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAccessTokenBlocklist accessTokenBlocklist)
    {
        var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;
        if (isAuthenticated)
        {
            var accessToken = BearerTokenReader.ReadAccessToken(context);
            if (!string.IsNullOrWhiteSpace(accessToken) && accessTokenBlocklist.IsBlocked(accessToken))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Access token has been invalidated." });
                return;
            }
        }

        await _next(context);
    }
}
