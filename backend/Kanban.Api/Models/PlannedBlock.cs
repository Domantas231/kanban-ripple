namespace Kanban.Api.Models;

public class PlannedBlock
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public Guid CardId { get; set; }
    public Card Card { get; set; } = null!;

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    /// <summary>
    /// IANA time zone (e.g. "Europe/Vilnius") the wall-clock Date/StartTime/EndTime
    /// were authored in. Used to resolve the block to an absolute UTC instant when
    /// computing elapsed/spent time. Null is treated as UTC for backward compatibility.
    /// </summary>
    public string? TimeZone { get; set; }

    public PlannedBlockSyncStatus SyncStatus { get; set; } = PlannedBlockSyncStatus.Pending;
    public string? GoogleEventId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
