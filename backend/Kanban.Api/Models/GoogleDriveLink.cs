namespace Kanban.Api.Models;

public class GoogleDriveLink
{
    public Guid Id { get; set; }

    public Guid CardId { get; set; }
    public Card Card { get; set; } = null!;

    public string GoogleFileId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string WebViewLink { get; set; } = string.Empty;
    public string? IconLink { get; set; }
    public string? ThumbnailLink { get; set; }
    public long? FileSize { get; set; }
    public DateTime? GoogleModifiedAt { get; set; }

    public Guid LinkedBy { get; set; }
    public ApplicationUser Linker { get; set; } = null!;

    public GoogleDriveSharePermission SharePermission { get; set; } = GoogleDriveSharePermission.Reader;

    public DateTime LinkedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
