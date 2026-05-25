using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Cards;
using Kanban.Api.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class CardsController : KanbanControllerBase
{
    private readonly ICardService _cardService;
    private readonly ICardActivityService _cardActivityService;

    public CardsController(ICardService cardService, ICardActivityService cardActivityService)
    {
        _cardService = cardService;
        _cardActivityService = cardActivityService;
    }

    [HttpGet("boards/{boardId:guid}/cards")]
    public async Task<ActionResult<PaginatedResponse<Card>>> ListByBoard(
        Guid boardId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _cardService.ListByBoardAsync(boardId, userId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("projects/{projectId:guid}/activities")]
    public async Task<ActionResult<List<ProjectActivityDto>>> ListProjectActivities(
        Guid projectId,
        [FromQuery] int limit = 30)
    {
        var userId = GetUserId();
        var result = await _cardActivityService.ListByProjectAsync(projectId, userId, limit);
        return Ok(result);
    }

    [HttpGet("projects/{projectId:guid}/cards/search")]
    public async Task<ActionResult<PaginatedResponse<Card>>> Search(
        Guid projectId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(q))
        {
            throw new BadRequestException("Query parameter 'q' is required.");
        }

        var result = await _cardService.SearchAsync(projectId, userId, q, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("boards/{boardId:guid}/cards/filter")]
    public async Task<ActionResult<IReadOnlyList<Card>>> Filter(
        Guid boardId,
        [FromQuery] string? tagIds,
        [FromQuery] string? userIds,
        [FromQuery] string? columnIds,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();

        if (!TryParseGuidList(tagIds, out var parsedTagIds)
            || !TryParseGuidList(userIds, out var parsedUserIds)
            || !TryParseGuidList(columnIds, out var parsedColumnIds))
        {
            throw new BadRequestException("Filter parameters must contain valid GUID values.");
        }

        var filters = new FilterCriteria(parsedTagIds, parsedUserIds, parsedColumnIds);
        var result = await _cardService.FilterAsync(boardId, userId, filters, cancellationToken);
        return Ok(result);
    }

    [HttpPost("columns/{columnId:guid}/cards")]
    public async Task<ActionResult<Card>> Create(Guid columnId, [FromBody] CreateCardRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _cardService.CreateAsync(
            columnId,
            userId,
            new CreateCardDto(request.Title, request.Description, request.StartDate, request.DueDate, request.EstimatedHours),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("cards/{id:guid}")]
    public async Task<ActionResult<Card>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _cardService.GetByIdAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("cards/{id:guid}/activities")]
    public async Task<ActionResult<List<CardActivity>>> ListActivities(Guid id)
    {
        var userId = GetUserId();
        var result = await _cardActivityService.ListByCardAsync(id, userId);
        return Ok(result);
    }

    [HttpPut("cards/{id:guid}")]
    public async Task<ActionResult<Card>> Update(Guid id, [FromBody] UpdateCardRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _cardService.UpdateAsync(
            id,
            userId,
            new UpdateCardDto(request.Title, request.Description, request.StartDate, request.DueDate, request.EstimatedHours, request.Version),
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("cards/{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _cardService.ArchiveAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("cards/{id:guid}/tags/{tagId:guid}")]
    public async Task<IActionResult> AssignTag(Guid id, Guid tagId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _cardService.AssignTagAsync(id, tagId, userId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("cards/{id:guid}/tags/{tagId:guid}")]
    public async Task<IActionResult> UnassignTag(Guid id, Guid tagId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _cardService.UnassignTagAsync(id, tagId, userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("cards/{id:guid}/assignees/{assigneeUserId:guid}")]
    public async Task<IActionResult> AssignUser(Guid id, Guid assigneeUserId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _cardService.AssignUserAsync(id, assigneeUserId, userId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("cards/{id:guid}/assignees/{assigneeUserId:guid}")]
    public async Task<IActionResult> UnassignUser(Guid id, Guid assigneeUserId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _cardService.UnassignUserAsync(id, assigneeUserId, userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("cards/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _cardService.RestoreAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("cards/{id:guid}/permanent")]
    public async Task<IActionResult> Purge(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _cardService.PurgeAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPut("cards/{id:guid}/schedule")]
    public async Task<ActionResult<Card>> Schedule(Guid id, [FromBody] ScheduleCardRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _cardService.ScheduleAsync(
            id,
            userId,
            new ScheduleCardDto(request.StartDate, request.DueDate),
            cancellationToken);

        return Ok(result);
    }

    [HttpPut("cards/{id:guid}/move")]
    public async Task<ActionResult<Card>> Move(Guid id, [FromBody] MoveCardRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _cardService.MoveAsync(id, userId, new MoveCardDto(request.ColumnId, request.Position), cancellationToken);
        return Ok(result);
    }

    [HttpGet("cards/archived")]
    public async Task<ActionResult<PaginatedResponse<Card>>> ListArchived(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _cardService.ListArchivedAsync(userId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("boards/{boardId:guid}/cards/archived")]
    public async Task<ActionResult<PaginatedResponse<Card>>> ListArchivedByBoard(
        Guid boardId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _cardService.ListArchivedByBoardAsync(boardId, userId, page, pageSize, cancellationToken);
        return Ok(result);
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
