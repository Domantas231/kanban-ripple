using Kanban.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Search;

public sealed class SearchService : ISearchService
{
    private const int MaxResultsPerType = 5;

    private readonly ApplicationDbContext _dbContext;

    public SearchService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GlobalSearchResult> SearchAsync(Guid userId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new GlobalSearchResult(Array.Empty<GlobalSearchItem>());
        }

        var normalizedQuery = query.Trim();

        var accessibleProjectIds = await _dbContext.ProjectMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.ProjectId)
            .ToListAsync();

        if (accessibleProjectIds.Count == 0)
        {
            return new GlobalSearchResult(Array.Empty<GlobalSearchItem>());
        }

        var providerName = _dbContext.Database.ProviderName;
        var isNpgsql = !string.IsNullOrWhiteSpace(providerName)
            && providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);

        var items = new List<GlobalSearchItem>();

        var projects = await SearchProjectsAsync(accessibleProjectIds, normalizedQuery, isNpgsql);
        items.AddRange(projects);

        var boards = await SearchBoardsAsync(accessibleProjectIds, normalizedQuery, isNpgsql);
        items.AddRange(boards);

        var columns = await SearchColumnsAsync(accessibleProjectIds, normalizedQuery, isNpgsql);
        items.AddRange(columns);

        var cards = await SearchCardsAsync(accessibleProjectIds, normalizedQuery, isNpgsql);
        items.AddRange(cards);

        return new GlobalSearchResult(items);
    }

    private async Task<List<GlobalSearchItem>> SearchProjectsAsync(
        List<Guid> projectIds, string query, bool isNpgsql)
    {
        var wildcard = $"%{query}%";

        var matchingProjects = isNpgsql
            ? await _dbContext.Projects
                .AsNoTracking()
                .Where(x => projectIds.Contains(x.Id) && EF.Functions.ILike(x.Name, wildcard))
                .OrderByDescending(x => x.UpdatedAt)
                .Take(MaxResultsPerType)
                .Select(x => new { x.Id, x.Name })
                .ToListAsync()
            : await _dbContext.Projects
                .AsNoTracking()
                .Where(x => projectIds.Contains(x.Id) && x.Name.ToLower().Contains(query.ToLower()))
                .OrderByDescending(x => x.UpdatedAt)
                .Take(MaxResultsPerType)
                .Select(x => new { x.Id, x.Name })
                .ToListAsync();

        return matchingProjects.Select(x => new GlobalSearchItem(
            x.Id, "project", x.Name, null, null)).ToList();
    }

    private async Task<List<GlobalSearchItem>> SearchBoardsAsync(
        List<Guid> projectIds, string query, bool isNpgsql)
    {
        var wildcard = $"%{query}%";

        var matchingBoards = isNpgsql
            ? await _dbContext.Boards
                .AsNoTracking()
                .Include(x => x.Project)
                .Where(x => projectIds.Contains(x.ProjectId) && EF.Functions.ILike(x.Name, wildcard))
                .OrderByDescending(x => x.UpdatedAt)
                .Take(MaxResultsPerType)
                .Select(x => new { x.Id, x.Name, x.ProjectId, ProjectName = x.Project!.Name })
                .ToListAsync()
            : await _dbContext.Boards
                .AsNoTracking()
                .Include(x => x.Project)
                .Where(x => projectIds.Contains(x.ProjectId) && x.Name.ToLower().Contains(query.ToLower()))
                .OrderByDescending(x => x.UpdatedAt)
                .Take(MaxResultsPerType)
                .Select(x => new { x.Id, x.Name, x.ProjectId, ProjectName = x.Project!.Name })
                .ToListAsync();

        return matchingBoards.Select(x => new GlobalSearchItem(
            x.Id, "board", x.Name, null,
            new GlobalSearchItemLocation(x.ProjectId, x.ProjectName, null, null, null, null))).ToList();
    }

    private async Task<List<GlobalSearchItem>> SearchColumnsAsync(
        List<Guid> projectIds, string query, bool isNpgsql)
    {
        var wildcard = $"%{query}%";

        var matchingColumns = isNpgsql
            ? await _dbContext.Columns
                .AsNoTracking()
                .Include(x => x.Board).ThenInclude(x => x.Project)
                .Where(x => projectIds.Contains(x.Board.ProjectId) && EF.Functions.ILike(x.Name, wildcard))
                .OrderByDescending(x => x.UpdatedAt)
                .Take(MaxResultsPerType)
                .Select(x => new { x.Id, x.Name, x.BoardId, BoardName = x.Board.Name, ProjectId = x.Board.ProjectId, ProjectName = x.Board.Project!.Name })
                .ToListAsync()
            : await _dbContext.Columns
                .AsNoTracking()
                .Include(x => x.Board).ThenInclude(x => x.Project)
                .Where(x => projectIds.Contains(x.Board.ProjectId) && x.Name.ToLower().Contains(query.ToLower()))
                .OrderByDescending(x => x.UpdatedAt)
                .Take(MaxResultsPerType)
                .Select(x => new { x.Id, x.Name, x.BoardId, BoardName = x.Board.Name, ProjectId = x.Board.ProjectId, ProjectName = x.Board.Project!.Name })
                .ToListAsync();

        return matchingColumns.Select(x => new GlobalSearchItem(
            x.Id, "column", x.Name, null,
            new GlobalSearchItemLocation(x.ProjectId, x.ProjectName, x.BoardId, x.BoardName, null, null))).ToList();
    }

    private async Task<List<GlobalSearchItem>> SearchCardsAsync(
        List<Guid> projectIds, string query, bool isNpgsql)
    {
        var wildcard = $"%{query}%";

        var matchingCards = isNpgsql
            ? await _dbContext.Cards
                .AsNoTracking()
                .Include(x => x.Column).ThenInclude(x => x.Board).ThenInclude(x => x.Project)
                .Where(x => projectIds.Contains(x.Column.Board.ProjectId)
                    && (EF.Functions.ILike(x.Title, wildcard)
                        || EF.Functions.ILike(x.Description ?? string.Empty, wildcard)))
                .OrderByDescending(x => x.UpdatedAt)
                .Take(MaxResultsPerType)
                .Select(x => new
                {
                    x.Id, x.Title, x.Description,
                    x.ColumnId, ColumnName = x.Column.Name,
                    BoardId = x.Column.BoardId, BoardName = x.Column.Board.Name,
                    ProjectId = x.Column.Board.ProjectId, ProjectName = x.Column.Board.Project!.Name
                })
                .ToListAsync()
            : await _dbContext.Cards
                .AsNoTracking()
                .Include(x => x.Column).ThenInclude(x => x.Board).ThenInclude(x => x.Project)
                .Where(x => projectIds.Contains(x.Column.Board.ProjectId)
                    && ((x.Title ?? string.Empty).ToLower().Contains(query.ToLower())
                        || (x.Description ?? string.Empty).ToLower().Contains(query.ToLower())))
                .OrderByDescending(x => x.UpdatedAt)
                .Take(MaxResultsPerType)
                .Select(x => new
                {
                    x.Id, x.Title, x.Description,
                    x.ColumnId, ColumnName = x.Column.Name,
                    BoardId = x.Column.BoardId, BoardName = x.Column.Board.Name,
                    ProjectId = x.Column.Board.ProjectId, ProjectName = x.Column.Board.Project!.Name
                })
                .ToListAsync();

        return matchingCards.Select(x => new GlobalSearchItem(
            x.Id, "card", x.Title, x.Description,
            new GlobalSearchItemLocation(x.ProjectId, x.ProjectName, x.BoardId, x.BoardName, x.ColumnId, x.ColumnName))).ToList();
    }
}
