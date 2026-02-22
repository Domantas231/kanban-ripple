using Microsoft.AspNetCore.Identity;

namespace Kanban.Api.Models;

public class ApplicationUser : IdentityUser<Guid>
{
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }

	public ICollection<ProjectMember> ProjectMemberships { get; set; } = new List<ProjectMember>();
	public ICollection<Card> CreatedCards { get; set; } = new List<Card>();
	public ICollection<Attachment> UploadedAttachments { get; set; } = new List<Attachment>();
}
