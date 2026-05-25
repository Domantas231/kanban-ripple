using Kanban.Api.Services.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class GoogleAuthController : KanbanControllerBase
{
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IGoogleCalendarService _calendarService;
    private readonly ILogger<GoogleAuthController> _logger;
    private readonly string _frontendUrl;

    public GoogleAuthController(
        IGoogleAuthService googleAuthService,
        IGoogleCalendarService calendarService,
        IConfiguration configuration,
        ILogger<GoogleAuthController> logger)
    {
        _googleAuthService = googleAuthService;
        _calendarService = calendarService;
        _logger = logger;
        _frontendUrl = configuration["Frontend:Url"]
            ?? throw new InvalidOperationException("Frontend:Url is missing.");
    }

    [HttpGet("google/auth")]
    public IActionResult Auth()
    {
        var userId = GetUserId();
        var url = _googleAuthService.BuildAuthUrl(userId);
        return Ok(new { url });
    }

    [HttpGet("google/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("Google OAuth returned error: {Error}", error);
            return Redirect($"{_frontendUrl}/settings?google=error");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            _logger.LogError("Google OAuth callback missing code or state. Code={Code}, State={State}", code is not null, state is not null);
            return Redirect($"{_frontendUrl}/settings?google=error");
        }

        var userId = _googleAuthService.ValidateState(state);
        if (userId is null)
        {
            _logger.LogError("Google OAuth state validation failed");
            return Redirect($"{_frontendUrl}/settings?google=error");
        }

        try
        {
            await _googleAuthService.ExchangeCodeAsync(code, userId.Value, cancellationToken);
            return Redirect($"{_frontendUrl}/settings?google=connected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google OAuth code exchange failed for user {UserId}", userId.Value);
            return Redirect($"{_frontendUrl}/settings?google=error");
        }
    }

    [HttpGet("google/status")]
    public async Task<ActionResult<GoogleConnectionStatusDto>> Status(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var status = await _googleAuthService.GetStatusAsync(userId, cancellationToken);
        return Ok(status);
    }

    [HttpGet("google/picker-token")]
    public async Task<IActionResult> PickerToken(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var accessToken = await _googleAuthService.GetAccessTokenAsync(userId, cancellationToken);
        return Ok(new { accessToken });
    }

    [HttpGet("google/calendar/events")]
    public async Task<ActionResult<IReadOnlyList<GoogleCalendarEventDto>>> CalendarEvents(
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var events = await _calendarService.GetEventsAsync(userId, date, cancellationToken);
        return Ok(events);
    }

    [HttpDelete("google/disconnect")]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await _googleAuthService.DisconnectAsync(userId, cancellationToken);
        return Ok();
    }
}
