namespace Kanban.Api.Services.Google;

public interface IGoogleDriveApiClient
{
    Task<GoogleFileMetadata> GetFileMetadataAsync(string accessToken, string fileId, CancellationToken cancellationToken = default);
    Task<List<GoogleFilePermission>> ListPermissionsAsync(string accessToken, string fileId, CancellationToken cancellationToken = default);
    Task<bool> AddPermissionAsync(string accessToken, string fileId, string email, string role = "reader", CancellationToken cancellationToken = default);
    Task<bool> UpdatePermissionAsync(string accessToken, string fileId, string permissionId, string role, CancellationToken cancellationToken = default);
    Task<bool> DeletePermissionAsync(string accessToken, string fileId, string permissionId, CancellationToken cancellationToken = default);
}

public sealed record GoogleFileMetadata(
    string Id,
    string Name,
    string MimeType,
    string WebViewLink,
    string? IconLink,
    string? ThumbnailLink,
    long? Size,
    DateTime? ModifiedTime
);

public sealed record GoogleFilePermission(
    string Id,
    string? EmailAddress,
    string Role,
    string Type
);
