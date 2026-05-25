using Kanban.Api.Models;
using Kanban.Api.Services.Attachments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class AttachmentsController : KanbanControllerBase
{
    private readonly IAttachmentService _attachmentService;

    public AttachmentsController(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    [HttpPost("cards/{cardId:guid}/attachments")]
    public async Task<ActionResult<Attachment>> Upload(Guid cardId, IFormFile file)
    {
        var userId = GetUserId();
        var result = await _attachmentService.AddAsync(cardId, userId, file);
        return Ok(result);
    }

    [HttpGet("attachments/{id:guid}")]
    public async Task<ActionResult<object>> GetDownloadUrl(Guid id)
    {
        var userId = GetUserId();
        var result = await _attachmentService.GetDownloadUrlAsync(id, userId);
        return Ok(new { url = result.Url, filename = result.Filename });
    }

    [HttpGet("attachments/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var userId = GetUserId();
        var result = await _attachmentService.GetDownloadStreamAsync(id, userId);
        return File(result.Content, result.MimeType, result.Filename);
    }

    [HttpDelete("attachments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        await _attachmentService.RemoveAsync(id, userId);
        return NoContent();
    }
}
