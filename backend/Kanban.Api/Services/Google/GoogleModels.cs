using Kanban.Api.Models;

namespace Kanban.Api.Services.Google;

public sealed class GoogleConnectionStatusDto
{
    public bool Connected { get; init; }
    public string? GoogleEmail { get; init; }
    public DateTime? ConnectedAt { get; init; }
}

public sealed record LinkGoogleDriveFilesRequest(List<string> GoogleFileIds, GoogleDriveSharePermission SharePermission = GoogleDriveSharePermission.Reader);

public sealed record UpdateDriveLinkPermissionRequest(GoogleDriveSharePermission SharePermission);

public sealed record GoogleDriveLinkDto(
    Guid Id,
    string GoogleFileId,
    string Name,
    string MimeType,
    string WebViewLink,
    string? IconLink,
    string? ThumbnailLink,
    long? FileSize,
    DateTime? GoogleModifiedAt,
    Guid LinkedBy,
    string LinkedByUserName,
    DateTime LinkedAt,
    GoogleDriveSharePermission SharePermission
);

public sealed record LinkFilesResultDto(
    List<GoogleDriveLinkDto> Links,
    PermissionReportDto PermissionReport
);

public sealed record PermissionReportDto(
    int SharedCount,
    int AlreadySharedCount,
    int FailedCount,
    List<string> FailedEmails
);

public sealed record PermissionRevokeReportDto(
    int RevokedCount,
    int FailedCount,
    List<string> FailedEmails
);
