namespace Kanban.Api.Configuration.Options;

public sealed class PlannerOptions
{
    public const string SectionName = "Planner";

    /// <summary>
    /// IANA time zone (e.g. "Europe/Vilnius") that planned-block wall-clock times are
    /// authored in. Used to resolve blocks to an absolute UTC instant when computing
    /// elapsed/spent time. Defaults to "UTC" for backward-compatible behavior.
    /// </summary>
    public string DefaultTimeZone { get; set; } = "UTC";
}
