using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kanban.Api.Models;
using Kanban.Api.Services;
using Kanban.Api.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpPost("subscriptions")]
    public async Task<ActionResult<Subscription>> Subscribe([FromBody] CreateSubscriptionRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            var subscription = await _subscriptionService.SubscribeAsync(userId, request.EntityType, request.EntityId);
            return Ok(subscription);
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

    [HttpDelete("subscriptions/{id:guid}")]
    public async Task<IActionResult> Unsubscribe(Guid id)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            await _subscriptionService.UnsubscribeByIdAsync(userId, id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("cards/{id:guid}/subscriptions")]
    public Task<ActionResult<IReadOnlyList<Guid>>> GetCardSubscriptions(Guid id)
    {
        return GetSubscriptionsByEntityAsync(EntityType.Card, id);
    }

    [HttpGet("columns/{id:guid}/subscriptions")]
    public Task<ActionResult<IReadOnlyList<Guid>>> GetColumnSubscriptions(Guid id)
    {
        return GetSubscriptionsByEntityAsync(EntityType.Column, id);
    }

    [HttpGet("projects/{id:guid}/subscriptions")]
    public Task<ActionResult<IReadOnlyList<Guid>>> GetProjectSubscriptions(Guid id)
    {
        return GetSubscriptionsByEntityAsync(EntityType.Project, id);
    }

    private async Task<ActionResult<IReadOnlyList<Guid>>> GetSubscriptionsByEntityAsync(EntityType entityType, Guid entityId)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid authenticated user." });
        }

        try
        {
            var subscriberIds = await _subscriptionService.GetSubscriberIdsAsync(userId, entityType, entityId);
            return Ok(subscriberIds);
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

    private bool TryGetUserId(out Guid userId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(userIdValue, out userId);
    }
}
