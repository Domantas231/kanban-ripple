using Kanban.Api.Services.Planner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class PlannerController : KanbanControllerBase
{
    private readonly IPlannerService _plannerService;

    public PlannerController(IPlannerService plannerService)
    {
        _plannerService = plannerService;
    }

    [HttpGet("projects/{projectId:guid}/planner/blocks")]
    public async Task<ActionResult<IReadOnlyList<PlannedBlockDto>>> GetBlocks(
        Guid projectId,
        [FromQuery] DateOnly date)
    {
        var userId = GetUserId();
        var result = await _plannerService.GetBlocksAsync(projectId, userId, date);
        return Ok(result);
    }

    [HttpPost("projects/{projectId:guid}/planner/blocks")]
    public async Task<ActionResult<PlannedBlockDto>> CreateBlock(
        Guid projectId,
        [FromBody] CreatePlannedBlockRequest request)
    {
        var userId = GetUserId();
        var result = await _plannerService.CreateBlockAsync(projectId, userId, request);
        return Ok(result);
    }

    [HttpPut("projects/{projectId:guid}/planner/blocks/{blockId:guid}")]
    public async Task<ActionResult<PlannedBlockDto>> UpdateBlock(
        Guid projectId,
        Guid blockId,
        [FromBody] UpdatePlannedBlockRequest request)
    {
        var userId = GetUserId();
        var result = await _plannerService.UpdateBlockAsync(blockId, userId, request);
        return Ok(result);
    }

    [HttpDelete("projects/{projectId:guid}/planner/blocks/{blockId:guid}")]
    public async Task<IActionResult> DeleteBlock(Guid projectId, Guid blockId)
    {
        var userId = GetUserId();
        await _plannerService.DeleteBlockAsync(blockId, userId);
        return NoContent();
    }

    [HttpGet("projects/{projectId:guid}/planner/unscheduled")]
    public async Task<ActionResult<IReadOnlyList<UnscheduledCardDto>>> GetUnscheduledCards(
        Guid projectId,
        [FromQuery] DateOnly date)
    {
        var userId = GetUserId();
        var result = await _plannerService.GetUnscheduledCardsAsync(projectId, userId, date);
        return Ok(result);
    }
}
