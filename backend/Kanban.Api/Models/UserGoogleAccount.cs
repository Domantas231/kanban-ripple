namespace Kanban.Api.Models;

public class UserGoogleAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string GoogleEmail { get; set; } = string.Empty;
    public string GoogleUserId { get; set; } = string.Empty;
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; }
    public DateTime ConnectedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
