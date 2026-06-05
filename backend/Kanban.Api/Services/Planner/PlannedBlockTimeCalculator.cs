namespace Kanban.Api.Services.Planner;

/// <summary>
/// Resolves planned-block wall-clock times to absolute UTC instants and computes how many
/// minutes of a block have already elapsed. Planned blocks are stored as local wall-clock
/// time (the value the user picked), so they must be interpreted in a known time zone before
/// being compared against <see cref="DateTime.UtcNow"/>.
/// </summary>
public static class PlannedBlockTimeCalculator
{
    /// <summary>
    /// Computes the scheduled and already-elapsed minutes for a single planned block.
    /// </summary>
    /// <param name="date">Wall-clock date of the block.</param>
    /// <param name="startTime">Wall-clock start time of the block.</param>
    /// <param name="endTime">Wall-clock end time of the block.</param>
    /// <param name="timeZone">IANA time zone the wall-clock values were authored in. Null/blank is treated as UTC.</param>
    /// <param name="utcNow">Current instant in UTC.</param>
    /// <returns>
    /// Scheduled = total block length in minutes (0 when invalid).
    /// Spent = elapsed portion in minutes (0 before start, full length after end).
    /// </returns>
    public static (int Scheduled, int Spent) Compute(
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        string? timeZone,
        DateTime utcNow)
    {
        var minutes = (int)Math.Round((endTime - startTime).TotalMinutes);
        if (minutes <= 0)
        {
            return (0, 0);
        }

        var blockStartUtc = ToUtc(date.ToDateTime(startTime, DateTimeKind.Unspecified), timeZone);
        var blockEndUtc = ToUtc(date.ToDateTime(endTime, DateTimeKind.Unspecified), timeZone);

        if (utcNow >= blockEndUtc)
        {
            return (minutes, minutes);
        }

        if (utcNow > blockStartUtc)
        {
            return (minutes, (int)Math.Round((utcNow - blockStartUtc).TotalMinutes));
        }

        return (minutes, 0);
    }

    private static DateTime ToUtc(DateTime wallClock, string? timeZone)
    {
        var unspecified = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);

        if (string.IsNullOrWhiteSpace(timeZone))
        {
            return DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);
        }

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Unknown zone on this host: fall back to treating the value as UTC.
            return DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);
        }
    }
}
