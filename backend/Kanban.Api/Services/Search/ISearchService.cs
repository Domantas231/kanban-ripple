namespace Kanban.Api.Services.Search;

public interface ISearchService
{
    Task<GlobalSearchResult> SearchAsync(Guid userId, string query);
}
