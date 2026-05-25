using Kanban.Api.Models;

namespace Kanban.Api.Services.Google;

public interface IGoogleDriveLinkService
{
    Task<LinkFilesResultDto> LinkFilesAsync(Guid cardId, Guid userId, List<string> googleFileIds, GoogleDriveSharePermission sharePermission = GoogleDriveSharePermission.Reader, CancellationToken cancellationToken = default);
    Task<PermissionRevokeReportDto> UnlinkAsync(Guid linkId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<GoogleDriveLinkDto>> GetLinksAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default);
    Task<GoogleDriveLinkDto> UpdatePermissionAsync(Guid linkId, Guid userId, GoogleDriveSharePermission sharePermission, CancellationToken cancellationToken = default);
    Task RevokePermissionsForCardsAsync(IReadOnlyCollection<Guid> cardIds, CancellationToken cancellationToken = default);
}
