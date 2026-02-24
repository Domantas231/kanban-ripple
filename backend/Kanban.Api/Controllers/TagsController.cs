using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kanban.Api.Models;
using Kanban.Api.Services;
using Kanban.Api.Services.Tags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class TagsController : ControllerBase
{
    private readonly ITagService _tagService;

    public TagsController(ITagService tagService)
    {
        _tagService = tagService;
    }

    [HttpGet("projects/{projectId:guid}/tags")]
    public async Task<ActionResult<IReadOnlyList<Tag>>> ListByProject(Guid projectId)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            var result = await _tagService.ListAsync(projectId, userId);
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

    [HttpPost("projects/{projectId:guid}/tags")]
    public async Task<ActionResult<Tag>> Create(Guid projectId, [FromBody] CreateTagRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            var result = await _tagService.CreateAsync(projectId, userId, new CreateTagDto(request.Name, request.Color));
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
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("tags/{id:guid}")]
    public async Task<ActionResult<Tag>> Update(Guid id, [FromBody] UpdateTagRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            var result = await _tagService.UpdateAsync(id, userId, new UpdateTagDto(request.Name, request.Color));
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
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("tags/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            await _tagService.DeleteAsync(id, userId);
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