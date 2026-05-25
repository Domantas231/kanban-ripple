using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Cards;

public sealed class CardActivityService : ICardActivityService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectAccessGuard _accessGuard;

    public CardActivityService(ApplicationDbContext dbContext, IProjectAccessGuard accessGuard)
    {
        _dbContext = dbContext;
        _accessGuard = accessGuard;
    }

    public async Task LogAsync(Guid cardId, Guid userId, ActivityAction action, string? field = null, string? oldValue = null, string? newValue = null)
    {
        _dbContext.CardActivities.Add(new CardActivity
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = userId,
            Action = action,
            Field = field,
            OldValue = Truncate(oldValue, 500),
            NewValue = Truncate(newValue, 500),
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<CardActivity>> ListByCardAsync(Guid cardId, Guid userId)
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

        return await _dbContext.CardActivities
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.CardId == cardId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<ProjectActivityDto>> ListByProjectAsync(Guid projectId, Guid userId, int limit = 30)
    {
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer);

        var cardActivities = await _dbContext.CardActivities
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Card)
                .ThenInclude(c => c.Column)
                    .ThenInclude(col => col.Board)
            .Where(a => a.Card.Column.Board.ProjectId == projectId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new ProjectActivityDto
            {
                Id = a.Id,
                EntityType = "card",
                CardId = a.CardId,
                CardTitle = a.Card.Title,
                BoardId = a.Card.Column.Board.Id,
                BoardName = a.Card.Column.Board.Name,
                ColumnName = a.Card.Column.Name,
                UserId = a.UserId,
                UserName = a.User.UserName ?? a.User.Email ?? "",
                Action = a.Action,
                Field = a.Field,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                CreatedAt = a.CreatedAt,
                EntityName = a.Card.Title
            })
            .ToListAsync();

        var boardActivities = await _dbContext.BoardActivities
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Board)
            .Where(a => a.Board.ProjectId == projectId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new ProjectActivityDto
            {
                Id = a.Id,
                EntityType = "board",
                BoardId = a.BoardId,
                BoardName = a.Board.Name,
                UserId = a.UserId,
                UserName = a.User.UserName ?? a.User.Email ?? "",
                Action = a.Action,
                Field = a.Field,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                CreatedAt = a.CreatedAt,
                EntityName = a.Board.Name
            })
            .ToListAsync();

        var projectActivities = await _dbContext.ProjectActivities
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Project)
            .Where(a => a.ProjectId == projectId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new ProjectActivityDto
            {
                Id = a.Id,
                EntityType = "workspace",
                UserId = a.UserId,
                UserName = a.User.UserName ?? a.User.Email ?? "",
                Action = a.Action,
                Field = a.Field,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                CreatedAt = a.CreatedAt,
                EntityName = a.Project.Name
            })
            .ToListAsync();

        return cardActivities
            .Concat(boardActivities)
            .Concat(projectActivities)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToList();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value is null || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
