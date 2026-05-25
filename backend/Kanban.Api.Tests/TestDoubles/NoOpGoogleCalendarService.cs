using Kanban.Api.Services.Google;

namespace Kanban.Api.Tests.TestDoubles;

/// <summary>
/// Stand-in for <see cref="IGoogleCalendarService"/> that returns deterministic placeholder
/// data so tests never reach real Google APIs.
/// </summary>
public sealed class NoOpGoogleCalendarService : IGoogleCalendarService
{
    public Task<string> CreateEventAsync(Guid userId, string title, string? description, DateTime start, DateTime end, string timeZone = "UTC", CancellationToken cancellationToken = default)
        => Task.FromResult($"test-event-{Guid.NewGuid():N}");

    public Task UpdateEventAsync(Guid userId, string eventId, string title, DateTime start, DateTime end, string timeZone = "UTC", CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteEventAsync(Guid userId, string eventId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<GoogleCalendarEventDto>> GetEventsAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GoogleCalendarEventDto>>([]);
}
