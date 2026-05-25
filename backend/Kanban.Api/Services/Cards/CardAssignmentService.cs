using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Activities;
using Kanban.Api.Services.Authorization;
using Kanban.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services.Cards;

public sealed class CardAssignmentService : ICardAssignmentService
{
    private const int MaxTagsPerCard = 3;

    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly IActivityRecorder _activityRecorder;

    public CardAssignmentService(
        ApplicationDbContext dbContext,
        INotificationService notificationService,
        IProjectAccessGuard accessGuard,
        IActivityRecorder activityRecorder)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _accessGuard = accessGuard;
        _activityRecorder = activityRecorder;
    }

    public async Task AssignTagAsync(Guid cardId, Guid tagId, Guid userId, CancellationToken cancellationToken = default)
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

        var tag = await _dbContext.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tagId, cancellationToken);

        if (tag is null)
        {
            throw new NotFoundException("Tag not found.");
        }

        var boardId = card.Column.Board.Id;
        if (tag.BoardId != boardId)
        {
            throw new BadRequestException("Tag belongs to a different board.");
        }

        var exists = await _dbContext.CardTags
            .AnyAsync(x => x.CardId == cardId && x.TagId == tagId, cancellationToken);

        if (exists)
        {
            return;
        }

        var currentTagCount = await _dbContext.CardTags
            .CountAsync(x => x.CardId == cardId, cancellationToken);

        if (currentTagCount >= MaxTagsPerCard)
        {
            throw new BadRequestException("A card cannot have more than 3 tags.");
        }

        _dbContext.CardTags.Add(new CardTag
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            TagId = tagId,
            CreatedAt = DateTime.UtcNow
        });

        _activityRecorder.RecordCard(cardId, userId, ActivityAction.Added, "tag", null, tag.Name);

        card.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UnassignTagAsync(Guid cardId, Guid tagId, Guid userId, CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }
        await _accessGuard.RequireAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Member);

        var cardTag = await _dbContext.CardTags
            .Include(x => x.Tag)
            .FirstOrDefaultAsync(x => x.CardId == cardId && x.TagId == tagId, cancellationToken);

        if (cardTag is null)
        {
            return;
        }

        _activityRecorder.RecordCard(cardId, userId, ActivityAction.Removed, "tag", cardTag.Tag.Name, null);

        _dbContext.CardTags.Remove(cardTag);
        card.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId, CancellationToken cancellationToken = default)
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

        var assigneeIsProjectMember = await _dbContext.ProjectMembers
            .AnyAsync(x => x.ProjectId == projectId && x.UserId == assigneeUserId, cancellationToken);

        if (!assigneeIsProjectMember)
        {
            throw new BadRequestException("Assigned user must be a project member.");
        }

        var exists = await _dbContext.CardAssignments
            .AnyAsync(x => x.CardId == cardId && x.UserId == assigneeUserId, cancellationToken);

        if (exists)
        {
            return;
        }

        var assigneeDisplay = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == assigneeUserId)
            .Select(x => x.UserName ?? x.Email)
            .FirstOrDefaultAsync(cancellationToken) ?? "Unknown";

        _dbContext.CardAssignments.Add(new CardAssignment
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = assigneeUserId,
            AssignedBy = userId,
            AssignedAt = DateTime.UtcNow
        });

        _activityRecorder.RecordCard(cardId, userId, ActivityAction.Added, "assignee", null, assigneeDisplay);

        card.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (assigneeUserId == userId)
        {
            return;
        }

        var assignerDisplay = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.UserName ?? x.Email)
            .FirstOrDefaultAsync(cancellationToken);

        var assignerLabel = string.IsNullOrWhiteSpace(assignerDisplay)
            ? "A user"
            : assignerDisplay;

        await _notificationService.CreateAsync(
            assigneeUserId,
            NotificationType.Assigned,
            $"Assigned: {card.Title}",
            $"{assignerLabel} assigned you to card '{card.Title}'.",
            entityType: EntityType.Card,
            entityId: card.Id,
            createdBy: userId);
    }

    public async Task UnassignUserAsync(Guid cardId, Guid assigneeUserId, Guid userId, CancellationToken cancellationToken = default)
    {
        var card = await _dbContext.Cards
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }
        await _accessGuard.RequireAccessAsync(card.Column.Board.ProjectId, userId, ProjectRole.Member);

        var assignment = await _dbContext.CardAssignments
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.CardId == cardId && x.UserId == assigneeUserId, cancellationToken);

        if (assignment is null)
        {
            return;
        }

        var removedName = assignment.User?.UserName ?? assignment.User?.Email ?? "Unknown";
        _activityRecorder.RecordCard(cardId, userId, ActivityAction.Removed, "assignee", removedName, null);

        _dbContext.CardAssignments.Remove(assignment);
        card.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

}
