namespace Kanban.Api.Models;

public class Subtask
{
    public Guid Id { get; set; }

    public Guid CardId { get; set; }
    public Card Card { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public int Position { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}