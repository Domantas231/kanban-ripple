using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kanban.Api.Models;
using Kanban.Api.Services.Cards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class SubtasksController : ControllerBase
{
    private readonly ICardService _cardService;

    public SubtasksController(ICardService cardService)
    {
        _cardService = cardService;
    }

    [HttpPost("cards/{cardId:guid}/subtasks")]
    public async Task<ActionResult<Subtask>> Create(Guid cardId, [FromBody] CreateSubtaskRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            var result = await _cardService.CreateSubtaskAsync(cardId, userId, new CreateSubtaskDto(request.Description));
            return CreatedAtAction(nameof(Create), new { cardId }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("subtasks/{id:guid}")]
    public async Task<ActionResult<Subtask>> Update(Guid id, [FromBody] UpdateSubtaskRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            var result = await _cardService.UpdateSubtaskAsync(id, userId, new UpdateSubtaskDto(request.Description, request.Completed));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("subtasks/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            await _cardService.DeleteSubtaskAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(userIdValue, out userId);
    }
}