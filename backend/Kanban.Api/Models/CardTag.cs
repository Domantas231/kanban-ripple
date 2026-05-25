namespace Kanban.Api.Models;

public class CardTag
{
    public Guid Id { get; set; }

    public Guid CardId { get; set; }
    public Card Card { get; set; } = null!;

    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}