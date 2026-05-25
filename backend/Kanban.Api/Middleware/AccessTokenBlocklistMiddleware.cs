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
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    error = new
                    {
                        code = "TOKEN_REVOKED",
                        message = "Access token has been invalidated.",
                        timestamp = DateTimeOffset.UtcNow,
                        requestId = context.TraceIdentifier,
                        validationErrors = (object?)null
                    }
                };

                await context.Response.WriteAsJsonAsync(response);
                return;
            }
        }

        await _next(context);
    }
}
