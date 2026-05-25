namespace Kanban.Api.Configuration.Options;

public sealed class InvitationOptions
{
    public const string SectionName = "Invitations";
    public int LifetimeDays { get; set; } = 7;
    public int CleanupIntervalHours { get; set; } = 24;
}
