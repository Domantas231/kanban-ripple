using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Google;

public sealed class GoogleDriveLinkService : IGoogleDriveLinkService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IGoogleDriveApiClient _googleDriveApiClient;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IActivityRecorder _activityRecorder;
    private readonly IProjectBroadcaster _projectBroadcaster;

    public GoogleDriveLinkService(
        ApplicationDbContext dbContext,
        IGoogleAuthService googleAuthService,
        IGoogleDriveApiClient googleDriveApiClient,
        IProjectBroadcaster projectBroadcaster,
        IProjectAccessGuard? accessGuard = null,
        IActivityRecorder? activityRecorder = null)
    {
        _dbContext = dbContext;
        _googleAuthService = googleAuthService;
        _googleDriveApiClient = googleDriveApiClient;
        _projectBroadcaster = projectBroadcaster;
        _accessGuard = accessGuard ?? new ProjectAccessGuard(dbContext);
        _activityRecorder = activityRecorder ?? new ActivityRecorder(dbContext);
    }

    public async Task<LinkFilesResultDto> LinkFilesAsync(Guid cardId, Guid userId, List<string> googleFileIds, GoogleDriveSharePermission sharePermission = GoogleDriveSharePermission.Reader, CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId, cancellationToken)
            ?? throw new NotFoundException("Card not found.");

        var projectId = card.Column.Board.ProjectId;
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Member, cancellationToken);

        var accessToken = await _googleAuthService.GetAccessTokenAsync(userId, cancellationToken);

        var existingFileIds = await _dbContext.GoogleDriveLinks
            .Where(l => l.CardId == cardId)
            .Select(l => l.GoogleFileId)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingFileIds);
        var createdLinks = new List<GoogleDriveLink>();

        foreach (var fileId in googleFileIds)
        {
            if (existingSet.Contains(fileId))
            {
                continue;
            }

            var metadata = await _googleDriveApiClient.GetFileMetadataAsync(accessToken, fileId, cancellationToken);

            var link = new GoogleDriveLink
            {
                Id = Guid.NewGuid(),
                CardId = cardId,
                GoogleFileId = metadata.Id,
                Name = metadata.Name,
                MimeType = metadata.MimeType,
                WebViewLink = metadata.WebViewLink,
                IconLink = metadata.IconLink,
                ThumbnailLink = metadata.ThumbnailLink,
                FileSize = metadata.Size,
                GoogleModifiedAt = metadata.ModifiedTime,
                LinkedBy = userId,
                SharePermission = sharePermission,
                LinkedAt = DateTime.UtcNow
            };

            _dbContext.GoogleDriveLinks.Add(link);
            createdLinks.Add(link);
            existingSet.Add(fileId);
        }

        foreach (var link in createdLinks)
        {
            _activityRecorder.RecordCard(cardId, userId, ActivityAction.Added, "google drive", null, link.Name);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (createdLinks.Count > 0)
        {
            await _projectBroadcaster.CardUpdated(projectId, card);
        }

        var permissionReport = await ShareWithProjectMembersAsync(accessToken, projectId, userId, createdLinks, sharePermission, cancellationToken);

        var linkerUserName = await _dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.UserName)
            .FirstAsync(cancellationToken);

        var linkDtos = createdLinks.Select(l => new GoogleDriveLinkDto(
            l.Id,
            l.GoogleFileId,
            l.Name,
            l.MimeType,
            l.WebViewLink,
            l.IconLink,
            l.ThumbnailLink,
            l.FileSize,
            l.GoogleModifiedAt,
            l.LinkedBy,
            linkerUserName!,
            l.LinkedAt,
            l.SharePermission
        )).ToList();

        return new LinkFilesResultDto(linkDtos, permissionReport);
    }

    public async Task<PermissionRevokeReportDto> UnlinkAsync(Guid linkId, Guid userId, CancellationToken cancellationToken = default)
    {
        var link = await _dbContext.GoogleDriveLinks
            .Include(l => l.Card)
                .ThenInclude(c => c.Column)
                    .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(l => l.Id == linkId, cancellationToken)
            ?? throw new NotFoundException("Google Drive link not found.");

        var projectId = link.Card.Column.Board.ProjectId;
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Member, cancellationToken);

        var isLinker = link.LinkedBy == userId;
        var isCardCreator = link.Card.CreatedBy == userId;
        if (!isLinker && !isCardCreator)
        {
            if (!await _accessGuard.HasAccessAsync(projectId, userId, ProjectRole.Manager, cancellationToken))
            {
                throw new ForbiddenException("Only the linker, the card creator, or a project manager can unlink a Google Drive file.");
            }
        }

        var revokeReport = await RevokeMemberPermissionsAsync(link, projectId, cancellationToken);

        _activityRecorder.RecordCard(link.CardId, userId, ActivityAction.Removed, "google drive", link.Name, null);
        link.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _projectBroadcaster.CardUpdated(projectId, link.Card);

        return revokeReport;
    }

    private async Task<PermissionRevokeReportDto> RevokeMemberPermissionsAsync(
        GoogleDriveLink link,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        // Use the original linker's access token: they had share rights when the file was linked.
        string accessToken;
        try
        {
            accessToken = await _googleAuthService.GetAccessTokenAsync(link.LinkedBy, cancellationToken);
        }
        catch (Exception ex) when (ex is NotFoundException or BadRequestException)
        {
            // Linker disconnected their Google account or their refresh token is invalid; nothing we can do.
            return new PermissionRevokeReportDto(0, 0, []);
        }

        var memberEmails = await _dbContext.ProjectMembers
            .Where(pm => pm.ProjectId == projectId && pm.UserId != link.LinkedBy)
            .Join(
                _dbContext.UserGoogleAccounts,
                pm => pm.UserId,
                ga => ga.UserId,
                (pm, ga) => ga.GoogleEmail)
            .ToListAsync(cancellationToken);

        if (memberEmails.Count == 0)
        {
            return new PermissionRevokeReportDto(0, 0, []);
        }

        var memberEmailSet = new HashSet<string>(memberEmails, StringComparer.OrdinalIgnoreCase);

        List<GoogleFilePermission> permissions;
        try
        {
            permissions = await _googleDriveApiClient.ListPermissionsAsync(accessToken, link.GoogleFileId, cancellationToken);
        }
        catch
        {
            return new PermissionRevokeReportDto(0, memberEmails.Count, memberEmails);
        }

        var revoked = 0;
        var failed = 0;
        var failedEmails = new List<string>();

        foreach (var permission in permissions)
        {
            if (permission.EmailAddress is null
                || string.Equals(permission.Role, "owner", StringComparison.OrdinalIgnoreCase)
                || !memberEmailSet.Contains(permission.EmailAddress))
            {
                continue;
            }

            try
            {
                var success = await _googleDriveApiClient.DeletePermissionAsync(accessToken, link.GoogleFileId, permission.Id, cancellationToken);
                if (success)
                {
                    revoked++;
                }
                else
                {
                    failed++;
                    if (!failedEmails.Contains(permission.EmailAddress))
                    {
                        failedEmails.Add(permission.EmailAddress);
                    }
                }
            }
            catch
            {
                failed++;
                if (!failedEmails.Contains(permission.EmailAddress))
                {
                    failedEmails.Add(permission.EmailAddress);
                }
            }
        }

        return new PermissionRevokeReportDto(revoked, failed, failedEmails);
    }

    public async Task<List<GoogleDriveLinkDto>> GetLinksAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .Include(c => c.Column)
                .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == cardId, cancellationToken)
            ?? throw new NotFoundException("Card not found.");

        var projectId = card.Column.Board.ProjectId;
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer, cancellationToken);

        return await _dbContext.GoogleDriveLinks
            .Where(l => l.CardId == cardId)
            .Include(l => l.Linker)
            .OrderBy(l => l.LinkedAt)
            .Select(l => new GoogleDriveLinkDto(
                l.Id,
                l.GoogleFileId,
                l.Name,
                l.MimeType,
                l.WebViewLink,
                l.IconLink,
                l.ThumbnailLink,
                l.FileSize,
                l.GoogleModifiedAt,
                l.LinkedBy,
                l.Linker.UserName!,
                l.LinkedAt,
                l.SharePermission
            ))
            .ToListAsync(cancellationToken);
    }

    private async Task<PermissionReportDto> ShareWithProjectMembersAsync(
        string accessToken,
        Guid projectId,
        Guid currentUserId,
        List<GoogleDriveLink> newLinks,
        GoogleDriveSharePermission sharePermission,
        CancellationToken cancellationToken)
    {
        if (newLinks.Count == 0)
        {
            return new PermissionReportDto(0, 0, 0, []);
        }

        var roleString = ToApiRole(sharePermission);

        var membersWithGoogle = await _dbContext.ProjectMembers
            .Where(pm => pm.ProjectId == projectId && pm.UserId != currentUserId)
            .Join(
                _dbContext.UserGoogleAccounts,
                pm => pm.UserId,
                ga => ga.UserId,
                (pm, ga) => ga.GoogleEmail)
            .ToListAsync(cancellationToken);

        if (membersWithGoogle.Count == 0)
        {
            return new PermissionReportDto(0, 0, 0, []);
        }

        var sharedCount = 0;
        var alreadySharedCount = 0;
        var failedCount = 0;
        var failedEmails = new List<string>();

        foreach (var link in newLinks)
        {
            List<GoogleFilePermission> existingPermissions;
            try
            {
                existingPermissions = await _googleDriveApiClient.ListPermissionsAsync(accessToken, link.GoogleFileId, cancellationToken);
            }
            catch
            {
                continue;
            }

            var existingByEmail = existingPermissions
                .Where(p => p.EmailAddress is not null)
                .ToDictionary(p => p.EmailAddress!, p => p, StringComparer.OrdinalIgnoreCase);

            foreach (var email in membersWithGoogle)
            {
                if (existingByEmail.TryGetValue(email, out var existing))
                {
                    if (TryParseApiRole(existing.Role) is { } existingRole && existingRole >= sharePermission)
                    {
                        alreadySharedCount++;
                        continue;
                    }

                    var upgraded = await _googleDriveApiClient.UpdatePermissionAsync(accessToken, link.GoogleFileId, existing.Id, roleString, cancellationToken);
                    if (upgraded)
                    {
                        sharedCount++;
                    }
                    else
                    {
                        failedCount++;
                        if (!failedEmails.Contains(email))
                        {
                            failedEmails.Add(email);
                        }
                    }
                    continue;
                }

                var success = await _googleDriveApiClient.AddPermissionAsync(accessToken, link.GoogleFileId, email, roleString, cancellationToken);
                if (success)
                {
                    sharedCount++;
                }
                else
                {
                    failedCount++;
                    if (!failedEmails.Contains(email))
                    {
                        failedEmails.Add(email);
                    }
                }
            }
        }

        return new PermissionReportDto(sharedCount, alreadySharedCount, failedCount, failedEmails);
    }

    public async Task<GoogleDriveLinkDto> UpdatePermissionAsync(Guid linkId, Guid userId, GoogleDriveSharePermission sharePermission, CancellationToken cancellationToken = default)
    {
        var link = await _dbContext.GoogleDriveLinks
            .Include(l => l.Card)
                .ThenInclude(c => c.Column)
                    .ThenInclude(col => col.Board)
            .Include(l => l.Linker)
            .FirstOrDefaultAsync(l => l.Id == linkId, cancellationToken)
            ?? throw new NotFoundException("Google Drive link not found.");

        var projectId = link.Card.Column.Board.ProjectId;
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Member, cancellationToken);

        if (link.LinkedBy != userId)
        {
            throw new ForbiddenException("Only the user who linked the file can change its share permission.");
        }

        var oldPermission = link.SharePermission;
        if (oldPermission == sharePermission)
        {
            return new GoogleDriveLinkDto(
                link.Id, link.GoogleFileId, link.Name, link.MimeType, link.WebViewLink,
                link.IconLink, link.ThumbnailLink, link.FileSize, link.GoogleModifiedAt,
                link.LinkedBy, link.Linker.UserName!, link.LinkedAt, link.SharePermission
            );
        }

        var accessToken = await _googleAuthService.GetAccessTokenAsync(userId, cancellationToken);
        var roleString = ToApiRole(sharePermission);

        var membersWithGoogle = await _dbContext.ProjectMembers
            .Where(pm => pm.ProjectId == projectId && pm.UserId != userId)
            .Join(
                _dbContext.UserGoogleAccounts,
                pm => pm.UserId,
                ga => ga.UserId,
                (pm, ga) => ga.GoogleEmail)
            .ToListAsync(cancellationToken);

        foreach (var email in membersWithGoogle)
        {
            try
            {
                var permissions = await _googleDriveApiClient.ListPermissionsAsync(accessToken, link.GoogleFileId, cancellationToken);
                var existing = permissions.FirstOrDefault(p =>
                    string.Equals(p.EmailAddress, email, StringComparison.OrdinalIgnoreCase));

                if (existing is not null)
                {
                    await _googleDriveApiClient.UpdatePermissionAsync(accessToken, link.GoogleFileId, existing.Id, roleString, cancellationToken);
                }
                else
                {
                    await _googleDriveApiClient.AddPermissionAsync(accessToken, link.GoogleFileId, email, roleString, cancellationToken);
                }
            }
            catch
            {
                // Best-effort: continue updating even if one member fails
            }
        }

        link.SharePermission = sharePermission;
        _activityRecorder.RecordCard(link.CardId, userId, ActivityAction.Changed, "google drive permission", ToApiRole(oldPermission), roleString);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _projectBroadcaster.CardUpdated(projectId, link.Card);

        return new GoogleDriveLinkDto(
            link.Id, link.GoogleFileId, link.Name, link.MimeType, link.WebViewLink,
            link.IconLink, link.ThumbnailLink, link.FileSize, link.GoogleModifiedAt,
            link.LinkedBy, link.Linker.UserName!, link.LinkedAt, link.SharePermission
        );
    }

    public async Task RevokePermissionsForCardsAsync(IReadOnlyCollection<Guid> cardIds, CancellationToken cancellationToken = default)
    {
        if (cardIds.Count == 0)
        {
            return;
        }

        var links = await _dbContext.GoogleDriveLinks
            .IgnoreQueryFilters()
            .Where(l => cardIds.Contains(l.CardId))
            .Include(l => l.Card)
                .ThenInclude(c => c.Column)
                    .ThenInclude(col => col.Board)
            .ToListAsync(cancellationToken);

        foreach (var link in links)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await RevokeMemberPermissionsAsync(link, link.Card.Column.Board.ProjectId, cancellationToken);
            }
            catch
            {
                // Best-effort: a single link failure must not abort the purge.
            }
        }
    }

    private static string ToApiRole(GoogleDriveSharePermission permission) =>
        permission.ToString().ToLowerInvariant();

    private static GoogleDriveSharePermission? TryParseApiRole(string role) =>
        Enum.TryParse<GoogleDriveSharePermission>(role, ignoreCase: true, out var parsed) ? parsed : null;

}
