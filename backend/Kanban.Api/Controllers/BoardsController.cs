using Kanban.Api.Models;
using Kanban.Api.Services.Boards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class BoardsController : KanbanControllerBase
{
    private readonly IBoardService _boardService;

    public BoardsController(IBoardService boardService)
    {
        _boardService = boardService;
    }

    [HttpGet("projects/{projectId:guid}/boards")]
    public async Task<ActionResult<IReadOnlyList<Board>>> ListByProject(Guid projectId)
    {
        var userId = GetUserId();
        var result = await _boardService.ListAsync(projectId, userId);
        return Ok(result);
    }

    [HttpPost("projects/{projectId:guid}/boards")]
    public async Task<ActionResult<Board>> Create(Guid projectId, [FromBody] CreateBoardRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _boardService.CreateAsync(projectId, userId, request.Name, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("projects/{projectId:guid}/boards/import-trello")]
    public async Task<ActionResult<Board>> ImportFromTrello(Guid projectId, [FromBody] TrelloImportRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _boardService.ImportFromTrelloAsync(projectId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("boards/{id:guid}")]
    public async Task<ActionResult<Board>> GetById(Guid id)
    {
        var userId = GetUserId();
        var result = await _boardService.GetByIdAsync(id, userId);
        return Ok(result);
    }

    [HttpPut("boards/{id:guid}")]
    public async Task<ActionResult<Board>> Update(Guid id, [FromBody] UpdateBoardRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _boardService.UpdateAsync(id, userId, new UpdateBoardDto(request.Name, request.Position), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("boards/{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _boardService.ArchiveAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("boards/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _boardService.RestoreAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("boards/{id:guid}/permanent")]
    public async Task<IActionResult> Purge(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _boardService.PurgeAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("boards/archived")]
    public async Task<ActionResult<IReadOnlyList<Board>>> ListArchived()
    {
        var userId = GetUserId();
        var result = await _boardService.ListArchivedAsync(userId);
        return Ok(result);
    }
}
