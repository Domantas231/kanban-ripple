using System.ComponentModel.DataAnnotations.Schema;

namespace Kanban.Api.Models;

public class Board
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    [NotMapped]
    public int ColumnCount { get; set; }

    [NotMapped]
    public int CardCount { get; set; }

    public ICollection<Column> Columns { get; set; } = new List<Column>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public ICollection<BoardActivity> Activities { get; set; } = new List<BoardActivity>();
}