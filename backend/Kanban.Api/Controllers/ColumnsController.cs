using Kanban.Api.Models;
using Kanban.Api.Services.Columns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class ColumnsController : KanbanControllerBase
{
    private readonly IColumnService _columnService;

    public ColumnsController(IColumnService columnService)
    {
        _columnService = columnService;
    }

    [HttpGet("boards/{boardId:guid}/columns")]
    public async Task<ActionResult<IReadOnlyList<Column>>> ListByBoard(Guid boardId)
    {
        var userId = GetUserId();
        var result = await _columnService.ListAsync(boardId, userId);
        return Ok(result);
    }

    [HttpPost("boards/{boardId:guid}/columns")]
    public async Task<ActionResult<Column>> Create(Guid boardId, [FromBody] CreateColumnRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _columnService.CreateAsync(boardId, userId, request.Name, cancellationToken);
        return Ok(result);
    }

    [HttpPut("columns/{id:guid}")]
    public async Task<ActionResult<Column>> Update(Guid id, [FromBody] UpdateColumnRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _columnService.UpdateAsync(id, userId, new UpdateColumnDto(request.Name), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("columns/{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _columnService.ArchiveAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("columns/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _columnService.RestoreAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("columns/{id:guid}/permanent")]
    public async Task<IActionResult> Purge(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _columnService.PurgeAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("boards/{boardId:guid}/columns/archived")]
    public async Task<ActionResult<IReadOnlyList<Column>>> ListArchivedByBoard(Guid boardId)
    {
        var userId = GetUserId();
        var result = await _columnService.ListArchivedByBoardAsync(boardId, userId);
        return Ok(result);
    }

    [HttpPut("columns/{id:guid}/reorder")]
    public async Task<ActionResult<Column>> Reorder(Guid id, [FromBody] ReorderColumnRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _columnService.ReorderAsync(
            id,
            userId,
            new ReorderColumnDto(request.BeforeColumnId, request.AfterColumnId),
            cancellationToken);

        return Ok(result);
    }
}
