using Kanban.Api.Services.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class GoogleDriveLinksController : KanbanControllerBase
{
    private readonly IGoogleDriveLinkService _linkService;

    public GoogleDriveLinksController(IGoogleDriveLinkService linkService)
    {
        _linkService = linkService;
    }

    [HttpGet("cards/{cardId:guid}/google-drive-links")]
    public async Task<ActionResult<List<GoogleDriveLinkDto>>> GetLinks(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var links = await _linkService.GetLinksAsync(cardId, userId, cancellationToken);
        return Ok(links);
    }

    [HttpPost("cards/{cardId:guid}/google-drive-links")]
    public async Task<ActionResult<LinkFilesResultDto>> LinkFiles(Guid cardId, LinkGoogleDriveFilesRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _linkService.LinkFilesAsync(cardId, userId, request.GoogleFileIds, request.SharePermission, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("google-drive-links/{linkId:guid}/permission")]
    public async Task<ActionResult<GoogleDriveLinkDto>> UpdatePermission(Guid linkId, UpdateDriveLinkPermissionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _linkService.UpdatePermissionAsync(linkId, userId, request.SharePermission, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("google-drive-links/{linkId:guid}")]
    public async Task<ActionResult<PermissionRevokeReportDto>> Unlink(Guid linkId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var report = await _linkService.UnlinkAsync(linkId, userId, cancellationToken);
        return Ok(report);
    }
}
