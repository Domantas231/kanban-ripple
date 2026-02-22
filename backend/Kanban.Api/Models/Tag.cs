namespace Kanban.Api.Models;

public class Tag
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<CardTag> CardTags { get; set; } = new List<CardTag>();
}