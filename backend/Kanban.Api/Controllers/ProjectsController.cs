using Kanban.Api.Models;
using Kanban.Api.Services.Invitations;
using Kanban.Api.Services.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class ProjectsController : KanbanControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IInvitationService _invitationService;

    public ProjectsController(IProjectService projectService, IInvitationService invitationService)
    {
        _projectService = projectService;
        _invitationService = invitationService;
    }

    [HttpGet("projects")]
    public async Task<ActionResult<PaginatedResponse<ProjectListItemDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var userId = GetUserId();
        var result = await _projectService.ListAsync(userId, page, pageSize);
        return Ok(result);
    }

    [HttpPost("projects")]
    public async Task<ActionResult<Project>> Create(
        [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _projectService.CreateAsync(userId, request.Name, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("projects/{id:guid}")]
    public async Task<ActionResult<Project>> GetById(Guid id)
    {
        var userId = GetUserId();
        var result = await _projectService.GetByIdAsync(id, userId);
        return Ok(result);
    }

    [HttpPut("projects/{id:guid}")]
    public async Task<ActionResult<Project>> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _projectService.UpdateAsync(id, userId, new UpdateProjectDto(request.Name), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("projects/{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _projectService.ArchiveAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("projects/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _projectService.RestoreAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("projects/{id:guid}/permanent")]
    public async Task<IActionResult> Purge(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _projectService.PurgeAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("projects/archived")]
    public async Task<ActionResult<PaginatedResponse<ProjectListItemDto>>> ListArchived(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var userId = GetUserId();
        var result = await _projectService.ListArchivedAsync(userId, page, pageSize);
        return Ok(result);
    }

    [HttpGet("projects/{id:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<ProjectMemberDto>>> GetMembers(Guid id)
    {
        var userId = GetUserId();
        var result = await _projectService.GetMembersAsync(id, userId);
        return Ok(result);
    }

    [HttpPut("projects/{id:guid}/members/{userId:guid}/role")]
    public async Task<ActionResult<ProjectMember>> UpdateMemberRole(
        Guid id,
        Guid userId,
        [FromBody] UpdateMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = GetUserId();
        var result = await _projectService.UpdateMemberRoleAsync(id, actorUserId, userId, request.Role!.Value, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("projects/{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var actorUserId = GetUserId();
        await _projectService.RemoveMemberAsync(id, actorUserId, userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("projects/{id:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(Guid id, [FromBody] TransferOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        var actorUserId = GetUserId();
        await _projectService.TransferOwnershipAsync(id, actorUserId, request.NewOwnerUserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("projects/{id:guid}/swimlane")]
    public async Task<ActionResult<SwimlaneView>> GetSwimlane(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _projectService.GetSwimlaneViewAsync(id, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("projects/{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await _projectService.LeaveAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpPost("projects/{id:guid}/invite")]
    public async Task<ActionResult<InvitationCreatedResponse>> Invite(Guid id, [FromBody] CreateInvitationRequest request)
    {
        var userId = GetUserId();
        await _invitationService.CreateInvitationAsync(id, userId, request.Email, request.Role);
        return Ok(new InvitationCreatedResponse("Invitation sent."));
    }
}
