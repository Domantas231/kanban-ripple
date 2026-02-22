using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Boards;

public sealed class BoardService : IBoardService
{
    private readonly ApplicationDbContext _dbContext;

    public BoardService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Board> CreateAsync(Guid projectId, Guid userId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Board name is required.");
        }

        var hasAccess = await CheckProjectAccessAsync(projectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var projectExists = await _dbContext.Projects.AnyAsync(x => x.Id == projectId);
        if (!projectExists)
        {
            throw new KeyNotFoundException("Project not found.");
        }

        var maxPosition = await _dbContext.Boards
            .Where(x => x.ProjectId == projectId)
            .Select(x => (int?)x.Position)
            .MaxAsync() ?? 0;

        var now = DateTime.UtcNow;
        var board = new Board
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name.Trim(),
            Position = maxPosition + 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Boards.Add(board);
        await _dbContext.SaveChangesAsync();

        return board;
    }

    public async Task<Board> GetByIdAsync(Guid boardId, Guid userId)
    {
        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == boardId);

        if (board is null)
        {
            throw new KeyNotFoundException("Board not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(board.ProjectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        return board;
    }

    public async Task<IReadOnlyList<Board>> ListAsync(Guid projectId, Guid userId)
    {
        var projectExists = await _dbContext.Projects.AnyAsync(x => x.Id == projectId);
        if (!projectExists)
        {
            throw new KeyNotFoundException("Project not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(projectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        return await _dbContext.Boards
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Board>> ListArchivedAsync(Guid userId)
    {
        return await _dbContext.Boards
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.DeletedAt != null)
            .Where(x => _dbContext.ProjectMembers.Any(pm =>
                pm.ProjectId == x.ProjectId
                && pm.UserId == userId
                && pm.Role <= ProjectRole.Viewer))
            .OrderByDescending(x => x.DeletedAt)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<Board> UpdateAsync(Guid boardId, Guid userId, UpdateBoardDto data)
    {
        if (string.IsNullOrWhiteSpace(data.Name))
        {
            throw new InvalidOperationException("Board name is required.");
        }

        var board = await _dbContext.Boards
            .FirstOrDefaultAsync(x => x.Id == boardId);

        if (board is null)
        {
            throw new KeyNotFoundException("Board not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        board.Name = data.Name.Trim();
        board.Position = data.Position;
        board.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return board;
    }

    public async Task ArchiveAsync(Guid boardId, Guid userId)
    {
        var board = await _dbContext.Boards
            .FirstOrDefaultAsync(x => x.Id == boardId);

        if (board is null)
        {
            throw new KeyNotFoundException("Board not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync()
            : null;

        var now = DateTime.UtcNow;
        board.DeletedAt = now;
        board.UpdatedAt = now;

        var columns = await _dbContext.Columns
            .Where(x => x.BoardId == board.Id)
            .ToListAsync();

        var columnIds = columns
            .Select(x => x.Id)
            .ToList();

        var cards = await _dbContext.Cards
            .Where(x => columnIds.Contains(x.ColumnId))
            .ToListAsync();

        foreach (var column in columns)
        {
            column.DeletedAt = now;
            column.UpdatedAt = now;
        }

        foreach (var card in cards)
        {
            card.DeletedAt = now;
            card.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync();

        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }
    }

    public async Task RestoreAsync(Guid boardId, Guid userId)
    {
        var board = await _dbContext.Boards
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == boardId);

        if (board is null)
        {
            throw new KeyNotFoundException("Board not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync()
            : null;

        var now = DateTime.UtcNow;
        board.DeletedAt = null;
        board.UpdatedAt = now;

        var columns = await _dbContext.Columns
            .IgnoreQueryFilters()
            .Where(x => x.BoardId == board.Id)
            .ToListAsync();

        var columnIds = columns
            .Select(x => x.Id)
            .ToList();

        var cards = await _dbContext.Cards
            .IgnoreQueryFilters()
            .Where(x => columnIds.Contains(x.ColumnId))
            .ToListAsync();

        foreach (var column in columns)
        {
            column.DeletedAt = null;
            column.UpdatedAt = now;
        }

        foreach (var card in cards)
        {
            card.DeletedAt = null;
            card.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync();

        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }
    }

    private async Task<bool> CheckProjectAccessAsync(Guid projectId, Guid userId, ProjectRole minimumRole)
    {
        var role = await _dbContext.ProjectMembers
            .Where(x => x.ProjectId == projectId && x.UserId == userId)
            .Select(x => (ProjectRole?)x.Role)
            .FirstOrDefaultAsync();

        if (role is null)
        {
            return false;
        }

        return HasRequiredRole(role.Value, minimumRole);
    }

    private static bool HasRequiredRole(ProjectRole actualRole, ProjectRole minimumRole)
    {
        return actualRole <= minimumRole;
    }
}
