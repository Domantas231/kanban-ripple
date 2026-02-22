namespace Kanban.Api.Models;

public class Column
{
    public Guid Id { get; set; }

    public Guid BoardId { get; set; }
    public Board Board { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<Card> Cards { get; set; } = new List<Card>();
}