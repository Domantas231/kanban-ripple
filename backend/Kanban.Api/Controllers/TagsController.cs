using Kanban.Api.Models;
using Kanban.Api.Services.Tags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class TagsController : KanbanControllerBase
{
    private readonly ITagService _tagService;

    public TagsController(ITagService tagService)
    {
        _tagService = tagService;
    }

    [HttpGet("boards/{boardId:guid}/tags")]
    public async Task<ActionResult<IReadOnlyList<Tag>>> ListByBoard(Guid boardId)
    {
        var userId = GetUserId();
        var result = await _tagService.ListAsync(boardId, userId);
        return Ok(result);
    }

    [HttpPost("boards/{boardId:guid}/tags")]
    public async Task<ActionResult<Tag>> Create(Guid boardId, [FromBody] CreateTagRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _tagService.CreateAsync(boardId, userId, new CreateTagDto(request.Name, request.Color), cancellationToken);
        return Ok(result);
    }

    [HttpPut("tags/{id:guid}")]
    public async Task<ActionResult<Tag>> Update(Guid id, [FromBody] UpdateTagRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _tagService.UpdateAsync(id, userId, new UpdateTagDto(request.Name, request.Color), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("tags/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _tagService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }
}
