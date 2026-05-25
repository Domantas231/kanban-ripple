using Kanban.Api.Services.Favorites;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class FavoritesController : KanbanControllerBase
{
    private readonly IFavoriteService _favoriteService;

    public FavoritesController(IFavoriteService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    [HttpGet("favorites")]
    public async Task<ActionResult<IReadOnlyList<FavoriteDto>>> List()
    {
        var userId = GetUserId();
        var favorites = await _favoriteService.ListAsync(userId);
        var dtos = favorites.Select(f => new FavoriteDto(f.Id, f.EntityType, f.EntityId, f.CreatedAt)).ToList();
        return Ok(dtos);
    }

    [HttpPost("favorites/toggle")]
    public async Task<ActionResult<FavoriteDto>> Toggle([FromBody] ToggleFavoriteRequest request)
    {
        var userId = GetUserId();
        var result = await _favoriteService.ToggleAsync(userId, request.EntityType, request.EntityId);
        return Ok(new FavoriteDto(result.Id, result.EntityType, result.EntityId, result.CreatedAt));
    }
}
