using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kanban.Api.Exceptions;

namespace Kanban.Api.Services.Google;

public sealed class GoogleCalendarService : IGoogleCalendarService
{
    private const string CalendarBaseUrl = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

    private readonly IGoogleAuthService _googleAuthService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(
        IGoogleAuthService googleAuthService,
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleCalendarService> logger)
    {
        _googleAuthService = googleAuthService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> CreateEventAsync(Guid userId, string title, string? description, DateTime start, DateTime end, string timeZone = "UTC", CancellationToken cancellationToken = default)
    {
        var accessToken = await _googleAuthService.GetAccessTokenAsync(userId, cancellationToken);
        using var client = _httpClientFactory.CreateClient("GoogleCalendarApi");

        var eventBody = new
        {
            summary = title,
            description,
            start = new { dateTime = start.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone },
            end = new { dateTime = end.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone }
        };

        var json = JsonSerializer.Serialize(eventBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, CalendarBaseUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new BadRequestException($"Failed to create Google Calendar event: {responseJson}");
        }

        var doc = JsonSerializer.Deserialize<JsonElement>(responseJson);
        return doc.GetProperty("id").GetString()!;
    }

    public async Task UpdateEventAsync(Guid userId, string eventId, string title, DateTime start, DateTime end, string timeZone = "UTC", CancellationToken cancellationToken = default)
    {
        var accessToken = await _googleAuthService.GetAccessTokenAsync(userId, cancellationToken);
        using var client = _httpClientFactory.CreateClient("GoogleCalendarApi");

        var eventBody = new
        {
            summary = title,
            start = new { dateTime = start.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone },
            end = new { dateTime = end.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone }
        };

        var json = JsonSerializer.Serialize(eventBody);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{CalendarBaseUrl}/{Uri.EscapeDataString(eventId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new BadRequestException($"Failed to update Google Calendar event '{eventId}': {responseJson}");
        }
    }

    public async Task DeleteEventAsync(Guid userId, string eventId, CancellationToken cancellationToken = default)
    {
        var accessToken = await _googleAuthService.GetAccessTokenAsync(userId, cancellationToken);
        using var client = _httpClientFactory.CreateClient("GoogleCalendarApi");

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{CalendarBaseUrl}/{Uri.EscapeDataString(eventId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new BadRequestException($"Failed to delete Google Calendar event '{eventId}': {responseJson}");
        }
    }

    public async Task<IReadOnlyList<GoogleCalendarEventDto>> GetEventsAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var accessToken = await _googleAuthService.GetAccessTokenAsync(userId, cancellationToken);
        using var client = _httpClientFactory.CreateClient("GoogleCalendarApi");

        var timeMin = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).ToString("o");
        var timeMax = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).ToString("o");

        var url = $"{CalendarBaseUrl}?timeMin={Uri.EscapeDataString(timeMin)}&timeMax={Uri.EscapeDataString(timeMax)}&singleEvents=true&orderBy=startTime";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new BadRequestException($"Failed to fetch Google Calendar events: {responseJson}");
        }

        var doc = JsonSerializer.Deserialize<JsonElement>(responseJson);

        if (!doc.TryGetProperty("items", out var items))
        {
            return [];
        }

        var events = new List<GoogleCalendarEventDto>();
        foreach (var item in items.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString()!;
            var summary = item.TryGetProperty("summary", out var s) ? s.GetString() : null;

            DateTime? startDt = null;
            DateTime? endDt = null;
            var isAllDay = false;

            if (item.TryGetProperty("start", out var startProp))
            {
                if (startProp.TryGetProperty("dateTime", out var startDateTime))
                {
                    startDt = DateTime.Parse(startDateTime.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind);
                }
                else if (startProp.TryGetProperty("date", out var startDate))
                {
                    startDt = DateTime.Parse(startDate.GetString()!);
                    isAllDay = true;
                }
            }

            if (item.TryGetProperty("end", out var endProp))
            {
                if (endProp.TryGetProperty("dateTime", out var endDateTime))
                {
                    endDt = DateTime.Parse(endDateTime.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind);
                }
                else if (endProp.TryGetProperty("date", out var endDate))
                {
                    endDt = DateTime.Parse(endDate.GetString()!);
                }
            }

            events.Add(new GoogleCalendarEventDto(id, summary, startDt, endDt, isAllDay));
        }

        return events;
    }
}
