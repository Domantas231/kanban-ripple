using System.ComponentModel.DataAnnotations.Schema;

namespace Kanban.Api.Models;

public class Card
{
    public Guid Id { get; set; }

    public Guid ColumnId { get; set; }
    public Column Column { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Position { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public double? EstimatedHours { get; set; }
    public int Version { get; set; } = 1;

    [NotMapped]
    public int ScheduledMinutes { get; set; }

    [NotMapped]
    public int SpentMinutes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Guid? CreatedBy { get; set; }
    public ApplicationUser? Creator { get; set; }

    public ICollection<CardTag> CardTags { get; set; } = new List<CardTag>();
    public ICollection<CardAssignment> Assignments { get; set; } = new List<CardAssignment>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    public ICollection<Subtask> Subtasks { get; set; } = new List<Subtask>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<GoogleDriveLink> GoogleDriveLinks { get; set; } = new List<GoogleDriveLink>();
    public ICollection<CardActivity> Activities { get; set; } = new List<CardActivity>();
}