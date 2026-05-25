namespace Kanban.Api.Models;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public Guid OwnerId { get; set; }
    public ApplicationUser Owner { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<Board> Boards { get; set; } = new List<Board>();
    public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
    public ICollection<ProjectActivity> Activities { get; set; } = new List<ProjectActivity>();
}