namespace Kanban.Api.Configuration.Options;

public sealed class ProfilePhotoOptions
{
    public const string SectionName = "ProfilePhoto";
    public long MaxFileSizeBytes { get; set; } = 2 * 1024 * 1024; // 2 MB
}
