using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Tags;

public sealed class TagService : ITagService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;

    public TagService(ApplicationDbContext dbContext, IProjectAccessGuard accessGuard)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
    }

    public async Task<Tag> CreateAsync(Guid boardId, Guid userId, CreateTagDto data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(data.Name))
        {
            throw new BadRequestException("Tag name is required.");
        }

        if (string.IsNullOrWhiteSpace(data.Color))
        {
            throw new BadRequestException("Tag color is required.");
        }

        var normalizedName = NormalizeName(data.Name);
        var normalizedColor = NormalizeColor(data.Color);

        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == boardId, cancellationToken);

        if (board is null)
        {
            throw new NotFoundException("Board not found.");
        }

        await _accessGuard.RequireAccessAsync(board.ProjectId, userId, ProjectRole.Member);

        var duplicateExists = await _dbContext.Tags
            .AnyAsync(x => x.BoardId == boardId && x.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (duplicateExists)
        {
            throw new ConflictException("Tag name must be unique within the board.", "DUPLICATE_NAME");
        }

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            BoardId = boardId,
            Name = normalizedName,
            Color = normalizedColor,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Tags.Add(tag);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return tag;
    }

    public async Task<Tag> GetByIdAsync(Guid tagId, Guid userId)
    {
        var tag = await _dbContext.Tags
            .AsNoTracking()
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == tagId);

        if (tag is null)
        {
            throw new NotFoundException("Tag not found.");
        }

        await _accessGuard.RequireAccessAsync(tag.Board.ProjectId, userId, ProjectRole.Viewer);

        return tag;
    }

    public async Task<IReadOnlyList<Tag>> ListAsync(Guid boardId, Guid userId)
    {
        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == boardId);

        if (board is null)
        {
            throw new NotFoundException("Board not found.");
        }

        await _accessGuard.RequireAccessAsync(board.ProjectId, userId, ProjectRole.Viewer);

        return await _dbContext.Tags
            .AsNoTracking()
            .Where(x => x.BoardId == boardId)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<Tag> UpdateAsync(Guid tagId, Guid userId, UpdateTagDto data, CancellationToken cancellationToken = default)
    {
        var tag = await _dbContext.Tags
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == tagId, cancellationToken);

        if (tag is null)
        {
            throw new NotFoundException("Tag not found.");
        }

        await _accessGuard.RequireAccessAsync(tag.Board.ProjectId, userId, ProjectRole.Member);

        if (data.Name is not null)
        {
            var normalizedName = NormalizeName(data.Name);
            var duplicateExists = await _dbContext.Tags
                .AnyAsync(x => x.BoardId == tag.BoardId
                    && x.Id != tag.Id
                    && x.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

            if (duplicateExists)
            {
                throw new ConflictException("Tag name must be unique within the board.", "DUPLICATE_NAME");
            }

            tag.Name = normalizedName;
        }

        if (data.Color is not null)
        {
            tag.Color = NormalizeColor(data.Color);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return tag;
    }

    public async Task DeleteAsync(Guid tagId, Guid userId, CancellationToken cancellationToken = default)
    {
        var tag = await _dbContext.Tags
            .Include(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == tagId, cancellationToken);

        if (tag is null)
        {
            throw new NotFoundException("Tag not found.");
        }

        await _accessGuard.RequireAccessAsync(tag.Board.ProjectId, userId, ProjectRole.Member);

        var cardTags = await _dbContext.CardTags
            .Where(x => x.TagId == tag.Id)
            .ToListAsync(cancellationToken);

        if (cardTags.Count > 0)
        {
            _dbContext.CardTags.RemoveRange(cardTags);
        }

        _dbContext.Tags.Remove(tag);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeName(string name) => name.Trim();

    private static string NormalizeColor(string color)
    {
        return color.Trim().ToUpperInvariant();
    }
}
