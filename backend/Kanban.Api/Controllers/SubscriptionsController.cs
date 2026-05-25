using Kanban.Api.Models;
using Kanban.Api.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class SubscriptionsController : KanbanControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("subscriptions/mine")]
    public async Task<ActionResult<IReadOnlyList<MySubscriptionDto>>> GetMySubscriptions()
    {
        var userId = GetUserId();
        var subscriptions = await _subscriptionService.GetMySubscriptionsAsync(userId);
        return Ok(subscriptions);
    }

    [HttpPost("subscriptions")]
    public async Task<ActionResult<Subscription>> Subscribe([FromBody] CreateSubscriptionRequest request)
    {
        var userId = GetUserId();
        var subscription = await _subscriptionService.SubscribeAsync(userId, request.EntityType, request.EntityId);
        return Ok(subscription);
    }

    [HttpDelete("subscriptions/{id:guid}")]
    public async Task<IActionResult> Unsubscribe(Guid id)
    {
        var userId = GetUserId();
        await _subscriptionService.UnsubscribeByIdAsync(userId, id);
        return NoContent();
    }

    [HttpDelete("subscriptions")]
    public async Task<IActionResult> UnsubscribeByEntity([FromQuery] string? entityType, [FromQuery] Guid entityId)
    {
        var userId = GetUserId();

        if (!TryParseEntityType(entityType, out var parsedEntityType))
        {
            return BadRequest(new { message = "Invalid entity type." });
        }

        await _subscriptionService.UnsubscribeAsync(userId, parsedEntityType, entityId);
        return NoContent();
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

    [HttpGet("boards/{id:guid}/subscriptions")]
    public Task<ActionResult<IReadOnlyList<Guid>>> GetBoardSubscriptions(Guid id)
    {
        return GetSubscriptionsByEntityAsync(EntityType.Board, id);
    }

    [HttpGet("projects/{id:guid}/subscriptions")]
    public Task<ActionResult<IReadOnlyList<Guid>>> GetProjectSubscriptions(Guid id)
    {
        return GetSubscriptionsByEntityAsync(EntityType.Project, id);
    }

    private async Task<ActionResult<IReadOnlyList<Guid>>> GetSubscriptionsByEntityAsync(EntityType entityType, Guid entityId)
    {
        var userId = GetUserId();
        var subscriberIds = await _subscriptionService.GetSubscriberIdsAsync(userId, entityType, entityId);
        return Ok(subscriberIds);
    }

    private static bool TryParseEntityType(string? value, out EntityType entityType)
    {
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<EntityType>(value, true, out entityType))
        {
            return true;
        }

        if (int.TryParse(value, out var numericValue) && Enum.IsDefined(typeof(EntityType), numericValue))
        {
            entityType = (EntityType)numericValue;
            return true;
        }

        entityType = default;
        return false;
    }
}
