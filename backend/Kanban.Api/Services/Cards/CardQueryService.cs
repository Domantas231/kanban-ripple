using Kanban.Api.Configuration.Options;
using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Authorization;
using Kanban.Api.Services.Planner;
using Kanban.Api.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kanban.Api.Services.Cards;

public sealed class CardQueryService : ICardQueryService
{
    private const int DefaultSearchCardsPageSize = 25;
    private const int MaxSearchCardsPageSize = 25;
    private const int DefaultArchivedCardsPageSize = 25;
    private const int MaxArchivedCardsPageSize = 50;
    private const int FilterResultCap = 300;

    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly string _plannerTimeZone;

    public CardQueryService(
        ApplicationDbContext dbContext,
        IProjectAccessGuard accessGuard,
        IOptions<PlannerOptions> plannerOptions)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
        _plannerTimeZone = plannerOptions.Value.DefaultTimeZone;
    }

    public async Task<PaginatedResponse<Card>> ListByBoardAsync(Guid boardId, Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var boardExists = await _dbContext.Boards
            .AsNoTracking()
            .AnyAsync(x => x.Id == boardId, cancellationToken);

        if (!boardExists)
        {
            throw new NotFoundException("Board not found.");
        }

        var boardProjectId = await _dbContext.Boards
            .AsNoTracking()
            .Where(x => x.Id == boardId)
            .Select(x => x.ProjectId)
            .FirstAsync(cancellationToken);
        await _accessGuard.RequireAccessAsync(boardProjectId, userId, ProjectRole.Viewer, cancellationToken);

        var query = _dbContext.Cards
            .AsNoTracking()
            .Where(x => x.Column.BoardId == boardId);

        var items = await query
            .Include(x => x.CardTags)
                .ThenInclude(x => x.Tag)
            .Include(x => x.Assignments)
                .ThenInclude(x => x.User)
            .Include(x => x.Subtasks)
            .Include(x => x.Attachments)
                .ThenInclude(x => x.Uploader)
            .Include(x => x.Comments)
            .Include(x => x.GoogleDriveLinks)
            .OrderBy(x => x.Column.Position)
            .ThenBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        await PopulatePlannedBlockMinutesAsync(items, cancellationToken);

        return new PaginatedResponse<Card>(items, 1, items.Count, items.Count);
    }

    public async Task<PaginatedResponse<Card>> SearchAsync(Guid projectId, Guid userId, string query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var projectExists = await _dbContext.Projects
            .AsNoTracking()
            .AnyAsync(x => x.Id == projectId, cancellationToken);

        if (!projectExists)
        {
            throw new NotFoundException("Project not found.");
        }
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer, cancellationToken);

        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize <= 0
            ? DefaultSearchCardsPageSize
            : Math.Min(pageSize, MaxSearchCardsPageSize);

        if (string.IsNullOrWhiteSpace(query))
        {
            return new PaginatedResponse<Card>(Array.Empty<Card>(), effectivePage, effectivePageSize, 0);
        }

        var normalizedQuery = query.Trim();

        var baseQuery = _dbContext.Cards
            .AsNoTracking()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .Where(x => x.Column.Board.ProjectId == projectId);

        IQueryable<Card> searchQuery;
        var providerName = _dbContext.Database.ProviderName;
        var isNpgsql = !string.IsNullOrWhiteSpace(providerName)
            && providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);

        if (isNpgsql)
        {
            var wildcardQuery = $"%{normalizedQuery}%";
            searchQuery = baseQuery.Where(x => EF.Functions.ILike(x.Title, wildcardQuery)
                || EF.Functions.ILike(x.Description ?? string.Empty, wildcardQuery));
        }
        else
        {
            var normalizedLower = normalizedQuery.ToLower();
            searchQuery = baseQuery.Where(x => (x.Title ?? string.Empty).ToLower().Contains(normalizedLower)
                || (x.Description ?? string.Empty).ToLower().Contains(normalizedLower));
        }

        var totalCount = await searchQuery.CountAsync(cancellationToken);

        var items = await searchQuery
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Id)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResponse<Card>(items, effectivePage, effectivePageSize, totalCount);
    }

    public async Task<List<Card>> FilterAsync(Guid boardId, Guid userId, FilterCriteria filters, CancellationToken cancellationToken = default)
    {
        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == boardId, cancellationToken);

        if (board is null)
        {
            throw new NotFoundException("Board not found.");
        }
        await _accessGuard.RequireAccessAsync(board.ProjectId, userId, ProjectRole.Viewer, cancellationToken);

        var tagIds = filters.TagIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        var userIds = filters.UserIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        var columnIds = filters.ColumnIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        var query = _dbContext.Cards
            .AsNoTracking()
            .Include(x => x.Column)
            .Include(x => x.CardTags)
                .ThenInclude(x => x.Tag)
            .Include(x => x.Assignments)
                .ThenInclude(x => x.User)
            .Include(x => x.Subtasks)
            .Include(x => x.Attachments)
                .ThenInclude(x => x.Uploader)
            .Include(x => x.Comments)
            .Include(x => x.GoogleDriveLinks)
            .Where(x => x.Column.BoardId == boardId)
            .AsQueryable();

        if (tagIds.Length > 0)
        {
            query = query.Where(x => x.CardTags.Any(cardTag => tagIds.Contains(cardTag.TagId)));
        }

        if (userIds.Length > 0)
        {
            query = query.Where(x => x.Assignments.Any(assignment => userIds.Contains(assignment.UserId)));
        }

        if (columnIds.Length > 0)
        {
            query = query.Where(x => columnIds.Contains(x.ColumnId));
        }

        return await query
            .OrderBy(x => x.Column.Position)
            .ThenBy(x => x.Position)
            .ThenBy(x => x.Id)
            .Take(FilterResultCap)
            .ToListAsync(cancellationToken);
    }

    public async Task<Card> GetByIdAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .AsNoTracking()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .Include(x => x.CardTags)
                .ThenInclude(x => x.Tag)
            .Include(x => x.Assignments)
                .ThenInclude(x => x.User)
            .Include(x => x.Attachments)
                .ThenInclude(x => x.Uploader)
            .Include(x => x.Subtasks)
            .Include(x => x.Creator)
            .Include(x => x.Comments)
            .Include(x => x.GoogleDriveLinks)
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }
        await _accessGuard.RequireAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Viewer, cancellationToken);

        return card;
    }

    public async Task<PaginatedResponse<Card>> ListArchivedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize <= 0
            ? DefaultArchivedCardsPageSize
            : Math.Min(pageSize, MaxArchivedCardsPageSize);

        var query = _dbContext.Cards
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .Where(x => x.DeletedAt != null)
            .Where(x => _dbContext.ProjectMembers.Any(pm =>
                pm.ProjectId == x.Column.Board.ProjectId
                && pm.UserId == userId
                && pm.Role <= ProjectRole.Member));

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.DeletedAt)
            .ThenBy(x => x.Id)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResponse<Card>(items, effectivePage, effectivePageSize, totalCount);
    }

    public async Task<PaginatedResponse<Card>> ListArchivedByBoardAsync(Guid boardId, Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == boardId, cancellationToken);

        if (board is null)
        {
            throw new NotFoundException("Board not found.");
        }
        await _accessGuard.RequireAccessAsync(board.ProjectId, userId, ProjectRole.Member, cancellationToken);

        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize <= 0
            ? DefaultArchivedCardsPageSize
            : Math.Min(pageSize, MaxArchivedCardsPageSize);

        var columnIds = await _dbContext.Columns
            .IgnoreQueryFilters()
            .Where(x => x.BoardId == boardId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var query = _dbContext.Cards
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Column)
            .Where(x => columnIds.Contains(x.ColumnId) && x.DeletedAt != null);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.DeletedAt)
            .ThenBy(x => x.Id)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResponse<Card>(items, effectivePage, effectivePageSize, totalCount);
    }

    private async Task PopulatePlannedBlockMinutesAsync(IReadOnlyList<Card> cards, CancellationToken cancellationToken)
    {
        if (cards.Count == 0)
        {
            return;
        }

        var cardIds = cards.Select(c => c.Id).ToList();

        var plannerRows = await _dbContext.PlannedBlocks
            .AsNoTracking()
            .Where(pb => cardIds.Contains(pb.CardId))
            .Select(pb => new { pb.CardId, pb.Date, pb.StartTime, pb.EndTime })
            .ToListAsync(cancellationToken);

        if (plannerRows.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;

        var aggregates = plannerRows
            .GroupBy(x => x.CardId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var scheduled = 0;
                    var spent = 0;
                    foreach (var row in g)
                    {
                        var (rowScheduled, rowSpent) = PlannedBlockTimeCalculator.Compute(
                            row.Date, row.StartTime, row.EndTime, _plannerTimeZone, now);
                        scheduled += rowScheduled;
                        spent += rowSpent;
                    }
                    return (Scheduled: scheduled, Spent: spent);
                });

        foreach (var card in cards)
        {
            if (aggregates.TryGetValue(card.Id, out var value))
            {
                card.ScheduledMinutes = value.Scheduled;
                card.SpentMinutes = value.Spent;
            }
            else
            {
                card.ScheduledMinutes = 0;
                card.SpentMinutes = 0;
            }
        }
    }
}
