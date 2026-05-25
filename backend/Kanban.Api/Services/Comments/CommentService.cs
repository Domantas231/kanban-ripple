using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Comments;

public sealed class CommentService : ICommentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IProjectBroadcaster _projectBroadcaster;
    private readonly IActivityRecorder _activityRecorder;

    public CommentService(
        ApplicationDbContext dbContext,
        IProjectAccessGuard accessGuard,
        IActivityRecorder activityRecorder,
        IProjectBroadcaster projectBroadcaster)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
        _activityRecorder = activityRecorder;
        _projectBroadcaster = projectBroadcaster;
    }

    public async Task<List<Comment>> ListByCardAsync(Guid cardId, Guid userId)
    {
        var card = await _dbContext.Cards
            .AsNoTracking()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }
        await _accessGuard.RequireAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Viewer);

        return await _dbContext.Comments
            .AsNoTracking()
            .Include(x => x.Author)
            .Where(x => x.CardId == cardId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Comment> CreateAsync(Guid cardId, Guid userId, CreateCommentDto data, CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }

        var projectId = card.Column.Board.ProjectId;
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Member);

        var now = DateTime.UtcNow;
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            AuthorId = userId,
            Content = data.Content.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Comments.Add(comment);
        _activityRecorder.RecordCard(cardId, userId, ActivityAction.Added, "comment");
        card.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await _dbContext.Comments
            .AsNoTracking()
            .Include(x => x.Author)
            .FirstAsync(x => x.Id == comment.Id, cancellationToken);

        await _projectBroadcaster.CommentCreated(projectId, created);

        return created;
    }

    public async Task<Comment> UpdateAsync(Guid commentId, Guid userId, UpdateCommentDto data, CancellationToken cancellationToken = default)
    {
        var comment = await _dbContext.Comments
            .Include(x => x.Card)
                .ThenInclude(x => x.Column)
                    .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken);

        if (comment is null)
        {
            throw new NotFoundException("Comment not found.");
        }

        if (comment.AuthorId != userId)
        {
            throw new ForbiddenException("Only the author can edit a comment.");
        }

        var projectId = comment.Card.Column.Board.ProjectId;
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Member);

        comment.Content = data.Content.Trim();
        comment.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await _dbContext.Comments
            .AsNoTracking()
            .Include(x => x.Author)
            .FirstAsync(x => x.Id == comment.Id, cancellationToken);

        await _projectBroadcaster.CommentUpdated(projectId, updated);

        return updated;
    }

    public async Task DeleteAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var comment = await _dbContext.Comments
            .Include(x => x.Card)
                .ThenInclude(x => x.Column)
                    .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken);

        if (comment is null)
        {
            throw new NotFoundException("Comment not found.");
        }

        var projectId = comment.Card.Column.Board.ProjectId;

        var isAuthor = comment.AuthorId == userId;
        if (!isAuthor)
        {
            if (!await _accessGuard.HasAccessAsync(projectId, userId, ProjectRole.Manager))
            {
                throw new ForbiddenException("Only the author or a project manager can delete a comment.");
            }
        }
        else
        {
            await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Member);
        }

        comment.DeletedAt = DateTime.UtcNow;
        comment.Card.UpdatedAt = DateTime.UtcNow;
        _activityRecorder.RecordCard(comment.CardId, userId, ActivityAction.Removed, "comment");

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _projectBroadcaster.CommentDeleted(projectId, commentId);
    }

}
