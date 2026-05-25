using Kanban.Api.Services.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Controllers;

[Authorize]
public sealed class SearchController : KanbanControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<GlobalSearchResult>> Search([FromQuery] string? q)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(new GlobalSearchResult(Array.Empty<GlobalSearchItem>()));
        }

        var result = await _searchService.SearchAsync(userId, q);
        return Ok(result);
    }
}
