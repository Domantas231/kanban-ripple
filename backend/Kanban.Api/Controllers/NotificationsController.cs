using Kanban.Api.Models;
using Kanban.Api.Services.Notifications;
using Kanban.Api.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class NotificationsController : KanbanControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<PaginatedResponse<Notification>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var result = await _notificationService.ListAsync(userId, page, pageSize);
        return Ok(result);
    }

    [HttpPut("notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetUserId();
        await _notificationService.MarkAsReadAsync(id, userId);
        return Ok();
    }

    [HttpPut("notifications/read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return NoContent();
    }

    [HttpDelete("notifications/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        await _notificationService.DeleteAsync(id, userId);
        return NoContent();
    }
}
