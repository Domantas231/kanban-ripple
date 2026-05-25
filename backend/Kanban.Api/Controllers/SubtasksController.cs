using Kanban.Api.Models;
using Kanban.Api.Services.Cards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class SubtasksController : KanbanControllerBase
{
    private readonly ICardService _cardService;

    public SubtasksController(ICardService cardService)
    {
        _cardService = cardService;
    }

    [HttpPost("cards/{cardId:guid}/subtasks")]
    public async Task<ActionResult<Subtask>> Create(Guid cardId, [FromBody] CreateSubtaskRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _cardService.CreateSubtaskAsync(cardId, userId, new CreateSubtaskDto(request.Description, request.Completed), cancellationToken);
        return CreatedAtAction(nameof(Create), new { cardId }, result);
    }

    [HttpPut("subtasks/{id:guid}")]
    public async Task<ActionResult<Subtask>> Update(Guid id, [FromBody] UpdateSubtaskRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _cardService.UpdateSubtaskAsync(id, userId, new UpdateSubtaskDto(request.Description, request.Completed, request.Position), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("subtasks/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _cardService.DeleteSubtaskAsync(id, userId, cancellationToken);
        return NoContent();
    }
}
