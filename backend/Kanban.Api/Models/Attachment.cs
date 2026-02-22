namespace Kanban.Api.Models;

public class Attachment
{
    public Guid Id { get; set; }

    public Guid CardId { get; set; }
    public Card Card { get; set; } = null!;

    public string Filename { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string? MimeType { get; set; }

    public Guid? UploadedBy { get; set; }
    public ApplicationUser? Uploader { get; set; }

    public DateTime UploadedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}