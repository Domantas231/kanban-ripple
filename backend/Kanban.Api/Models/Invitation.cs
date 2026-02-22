namespace Kanban.Api.Models;

public class Invitation
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;

    public Guid InvitedBy { get; set; }
    public ApplicationUser Inviter { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    public Guid? AcceptedBy { get; set; }
    public ApplicationUser? Accepter { get; set; }
}