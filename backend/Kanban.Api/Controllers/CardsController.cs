using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kanban.Api.Models;
using Kanban.Api.Services;
using Kanban.Api.Services.Cards;
using Kanban.Api.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class CardsController : ControllerBase
{
    private readonly ICardService _cardService;

    public CardsController(ICardService cardService)
    {
        _cardService = cardService;
    }

    [HttpGet("boards/{boardId:guid}/cards")]
    public async Task<ActionResult<PaginatedResponse<Card>>> ListByBoard(
        Guid boardId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            var result = await _cardService.ListByBoardAsync(boardId, userId, page, pageSize);
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
    }

    [HttpGet("projects/{projectId:guid}/cards/search")]
    public async Task<ActionResult<PaginatedResponse<Card>>> Search(
        Guid projectId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Query parameter 'q' is required." });
        }

        try
        {
            var result = await _cardService.SearchAsync(projectId, userId, q, page, pageSize);
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
    }

    [HttpGet("boards/{boardId:guid}/cards/filter")]
    public async Task<ActionResult<IReadOnlyList<Card>>> Filter(
        Guid boardId,
        [FromQuery] string? tagIds,
        [FromQuery] string? userIds,
        [FromQuery] string? columnIds)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        if (!TryParseGuidList(tagIds, out var parsedTagIds)
            || !TryParseGuidList(userIds, out var parsedUserIds)
            || !TryParseGuidList(columnIds, out var parsedColumnIds))
        {
            return BadRequest(new { message = "Filter parameters must contain valid GUID values." });
        }

        try
        {
            var filters = new FilterCriteria(parsedTagIds, parsedUserIds, parsedColumnIds);
            var result = await _cardService.FilterAsync(boardId, userId, filters);
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
    }

    [HttpPost("columns/{columnId:guid}/cards")]
    public async Task<ActionResult<Card>> Create(Guid columnId, [FromBody] CreateCardRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            var result = await _cardService.CreateAsync(
                columnId,
                userId,
                new CreateCardDto(request.Title, request.Description, request.PlannedDurationHours));

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
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

    [HttpGet("cards/{id:guid}")]
    public async Task<ActionResult<Card>> GetById(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            var result = await _cardService.GetByIdAsync(id, userId);
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
    }

    [HttpPut("cards/{id:guid}")]
    public async Task<ActionResult<Card>> Update(Guid id, [FromBody] UpdateCardRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            var result = await _cardService.UpdateAsync(
                id,
                userId,
                new UpdateCardDto(request.Title, request.Description, request.PlannedDurationHours, request.Version));

            return Ok(result);
        }
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message });
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

    [HttpDelete("cards/{id:guid}")]
    public async Task<IActionResult> Archive(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            await _cardService.ArchiveAsync(id, userId);
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

    [HttpPost("cards/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            await _cardService.RestoreAsync(id, userId);
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

    [HttpPut("cards/{id:guid}/move")]
    public async Task<ActionResult<Card>> Move(Guid id, [FromBody] MoveCardRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            var result = await _cardService.MoveAsync(id, userId, new MoveCardDto(request.ColumnId, request.Position));
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

    [HttpGet("cards/archived")]
    public async Task<ActionResult<PaginatedResponse<Card>>> ListArchived(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        var result = await _cardService.ListArchivedAsync(userId, page, pageSize);
        return Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(userIdValue, out userId);
    }

    private static bool TryParseGuidList(string? value, out IReadOnlyCollection<Guid>? parsedValues)
    {
        parsedValues = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var items = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (items.Length == 0)
        {
            return true;
        }

        var parsed = new List<Guid>(items.Length);
        foreach (var item in items)
        {
            if (!Guid.TryParse(item, out var id))
            {
                parsedValues = null;
                return false;
            }

            parsed.Add(id);
        }

        parsedValues = parsed;
        return true;
    }
}