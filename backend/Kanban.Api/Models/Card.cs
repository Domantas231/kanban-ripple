namespace Kanban.Api.Models;

public class Card
{
    public Guid Id { get; set; }

    public Guid ColumnId { get; set; }
    public Column Column { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Position { get; set; }
    public decimal? PlannedDurationHours { get; set; }
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Guid? CreatedBy { get; set; }
    public ApplicationUser? Creator { get; set; }

    public ICollection<CardTag> CardTags { get; set; } = new List<CardTag>();
    public ICollection<CardAssignment> Assignments { get; set; } = new List<CardAssignment>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    public ICollection<Subtask> Subtasks { get; set; } = new List<Subtask>();
}