namespace Kanban.Api.Models;

public class ColumnActivity
{
    public Guid Id { get; set; }

    public Guid ColumnId { get; set; }
    public Column Column { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public ActivityAction Action { get; set; }
    public string? Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public DateTime CreatedAt { get; set; }
}
