using Kanban.Api.Models;
using Kanban.Api.Services.Comments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class CommentsController : KanbanControllerBase
{
    private readonly ICommentService _commentService;

    public CommentsController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    [HttpGet("cards/{cardId:guid}/comments")]
    public async Task<ActionResult<List<Comment>>> ListByCard(Guid cardId)
    {
        var userId = GetUserId();
        var result = await _commentService.ListByCardAsync(cardId, userId);
        return Ok(result);
    }

    [HttpPost("cards/{cardId:guid}/comments")]
    public async Task<ActionResult<Comment>> Create(Guid cardId, [FromBody] CreateCommentRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _commentService.CreateAsync(cardId, userId, new CreateCommentDto(request.Content), cancellationToken);
        return Ok(result);
    }

    [HttpPut("comments/{id:guid}")]
    public async Task<ActionResult<Comment>> Update(Guid id, [FromBody] UpdateCommentRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _commentService.UpdateAsync(id, userId, new UpdateCommentDto(request.Content), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("comments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _commentService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }
}
