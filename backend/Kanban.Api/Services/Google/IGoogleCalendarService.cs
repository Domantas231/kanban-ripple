namespace Kanban.Api.Services.Google;

public interface IGoogleCalendarService
{
    Task<string> CreateEventAsync(Guid userId, string title, string? description, DateTime start, DateTime end, string timeZone = "UTC", CancellationToken cancellationToken = default);
    Task UpdateEventAsync(Guid userId, string eventId, string title, DateTime start, DateTime end, string timeZone = "UTC", CancellationToken cancellationToken = default);
    Task DeleteEventAsync(Guid userId, string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoogleCalendarEventDto>> GetEventsAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default);
}

public sealed record GoogleCalendarEventDto(
    string Id,
    string? Summary,
    DateTime? Start,
    DateTime? End,
    bool IsAllDay);
