namespace Kanban.Api.Models;

public class Tag
{
    public Guid Id { get; set; }

    public Guid BoardId { get; set; }
    public Board Board { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<CardTag> CardTags { get; set; } = new List<CardTag>();
}