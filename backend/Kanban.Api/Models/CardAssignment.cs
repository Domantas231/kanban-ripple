namespace Kanban.Api.Models;

public class CardAssignment
{
    public Guid Id { get; set; }

    public Guid CardId { get; set; }
    public Card Card { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public DateTime AssignedAt { get; set; }
    public Guid? AssignedBy { get; set; }
    public ApplicationUser? Assigner { get; set; }
}