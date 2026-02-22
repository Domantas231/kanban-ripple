using System.Data;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Columns;

public sealed class ColumnService : IColumnService
{
    private const int PositionGap = 1000;

    private readonly ApplicationDbContext _dbContext;

    public ColumnService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Column> CreateAsync(Guid boardId, Guid userId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Column name is required.");
        }

        var board = await _dbContext.Boards
            .AsNoTracking()
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

        var maxPosition = await _dbContext.Columns
            .Where(x => x.BoardId == boardId)
            .Select(x => (int?)x.Position)
            .MaxAsync();

        var nextPosition = (maxPosition ?? 0) + PositionGap;
        var now = DateTime.UtcNow;

        var column = new Column
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            Name = name.Trim(),
            Position = nextPosition,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Columns.Add(column);
        await _dbContext.SaveChangesAsync();

        return column;
    }

    public async Task<Column> GetByIdAsync(Guid columnId, Guid userId)
    {
        var column = await _dbContext.Columns
            .AsNoTracking()
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId);

        if (column is null)
        {
            throw new KeyNotFoundException("Column not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(column.Board.ProjectId, userId, ProjectRole.Viewer);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        return column;
    }

    public async Task<IReadOnlyList<Column>> ListAsync(Guid boardId, Guid userId)
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

        return await _dbContext.Columns
            .AsNoTracking()
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<Column> UpdateAsync(Guid columnId, Guid userId, UpdateColumnDto data)
    {
        if (string.IsNullOrWhiteSpace(data.Name))
        {
            throw new InvalidOperationException("Column name is required.");
        }

        var column = await _dbContext.Columns
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId);

        if (column is null)
        {
            throw new KeyNotFoundException("Column not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        column.Name = data.Name.Trim();
        column.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return column;
    }

    public async Task<Column> ReorderAsync(Guid columnId, Guid userId, ReorderColumnDto data)
    {
        if (data.BeforeColumnId is null && data.AfterColumnId is null)
        {
            throw new InvalidOperationException("At least one anchor column is required.");
        }

        if (data.BeforeColumnId == columnId || data.AfterColumnId == columnId)
        {
            throw new InvalidOperationException("A column cannot be used as its own anchor.");
        }

        if (data.BeforeColumnId is not null && data.BeforeColumnId == data.AfterColumnId)
        {
            throw new InvalidOperationException("Before and after anchors must be different.");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;

        var column = await _dbContext.Columns
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId);

        if (column is null)
        {
            throw new KeyNotFoundException("Column not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        var columns = await _dbContext.Columns
            .Where(x => x.BoardId == column.BoardId)
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Id)
            .ToListAsync();

        if (columns.Count <= 1)
        {
            column.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }

            return column;
        }

        var movable = columns.First(x => x.Id == columnId);
        var otherColumns = columns.Where(x => x.Id != columnId).ToList();

        Column? before = null;
        Column? after = null;

        if (data.BeforeColumnId is not null)
        {
            before = otherColumns.FirstOrDefault(x => x.Id == data.BeforeColumnId.Value)
                ?? throw new InvalidOperationException("Before anchor column not found in board.");
        }

        if (data.AfterColumnId is not null)
        {
            after = otherColumns.FirstOrDefault(x => x.Id == data.AfterColumnId.Value)
                ?? throw new InvalidOperationException("After anchor column not found in board.");
        }

        if (before is not null && after is not null)
        {
            var beforeIndex = otherColumns.FindIndex(x => x.Id == before.Id);
            var afterIndex = otherColumns.FindIndex(x => x.Id == after.Id);
            if (beforeIndex >= afterIndex)
            {
                throw new InvalidOperationException("Before anchor must appear before after anchor.");
            }
        }

        var now = DateTime.UtcNow;
        var requiresRenumber = false;
        int newPosition;

        if (before is not null && after is not null)
        {
            var gap = after.Position - before.Position;
            requiresRenumber = gap < 2;
            newPosition = (before.Position + after.Position) / 2;
        }
        else if (before is null && after is not null)
        {
            newPosition = after.Position - PositionGap;
            requiresRenumber = otherColumns.Any(x => x.Position == newPosition);
        }
        else
        {
            newPosition = before!.Position + PositionGap;
            requiresRenumber = otherColumns.Any(x => x.Position == newPosition);
        }

        if (!requiresRenumber)
        {
            movable.Position = newPosition;
            movable.UpdatedAt = now;
            await _dbContext.SaveChangesAsync();

            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }

            return movable;
        }

        var reordered = BuildOrderedColumns(otherColumns, movable, before?.Id, after?.Id);

        for (var index = 0; index < reordered.Count; index++)
        {
            reordered[index].Position = (index + 1) * PositionGap;
            reordered[index].UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync();

        if (transaction is not null)
        {
            await transaction.CommitAsync();
        }

        return movable;
    }

    public async Task ArchiveAsync(Guid columnId, Guid userId)
    {
        var column = await _dbContext.Columns
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId);

        if (column is null)
        {
            throw new KeyNotFoundException("Column not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync()
            : null;

        var now = DateTime.UtcNow;
        column.DeletedAt = now;
        column.UpdatedAt = now;

        var cards = await _dbContext.Cards
            .Where(x => x.ColumnId == column.Id)
            .ToListAsync();

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

    public async Task RestoreAsync(Guid columnId, Guid userId)
    {
        var column = await _dbContext.Columns
            .IgnoreQueryFilters()
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == columnId);

        if (column is null)
        {
            throw new KeyNotFoundException("Column not found.");
        }

        var hasAccess = await CheckProjectAccessAsync(column.Board.ProjectId, userId, ProjectRole.Member);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Forbidden.");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync()
            : null;

        var now = DateTime.UtcNow;
        column.DeletedAt = null;
        column.UpdatedAt = now;

        var cards = await _dbContext.Cards
            .IgnoreQueryFilters()
            .Where(x => x.ColumnId == column.Id)
            .ToListAsync();

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

    private static List<Column> BuildOrderedColumns(
        IReadOnlyList<Column> otherColumns,
        Column moving,
        Guid? beforeId,
        Guid? afterId)
    {
        var ordered = new List<Column>(otherColumns);

        if (beforeId is null && afterId is not null)
        {
            var insertBeforeIndex = ordered.FindIndex(x => x.Id == afterId.Value);
            ordered.Insert(insertBeforeIndex, moving);
            return ordered;
        }

        if (beforeId is not null && afterId is null)
        {
            var insertAfterIndex = ordered.FindIndex(x => x.Id == beforeId.Value);
            ordered.Insert(insertAfterIndex + 1, moving);
            return ordered;
        }

        var afterAnchorIndex = ordered.FindIndex(x => x.Id == afterId!.Value);
        ordered.Insert(afterAnchorIndex, moving);
        return ordered;
    }
}
