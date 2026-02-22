namespace Kanban.Api.Models;

public class Subscription
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }

    public DateTime CreatedAt { get; set; }
}