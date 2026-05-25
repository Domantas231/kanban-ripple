using Kanban.Api.Configuration.Options;
using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Hubs;
using Kanban.Api.Models;
using Kanban.Api.Services.Authorization;
using Kanban.Api.Services.Google;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kanban.Api.Services.Planner;

public sealed class PlannerService : IPlannerService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IGoogleCalendarService _calendarService;
    private readonly IProjectBroadcaster _broadcaster;
    private readonly ILogger<PlannerService> _logger;
    private readonly IProjectAccessGuard _accessGuard;
    private readonly string _frontendUrl;

    public PlannerService(
        ApplicationDbContext dbContext,
        IGoogleCalendarService calendarService,
        IProjectBroadcaster broadcaster,
        ILogger<PlannerService> logger,
        IOptions<FrontendOptions> frontendOptions,
        IProjectAccessGuard accessGuard)
    {
        _dbContext = dbContext;
        _calendarService = calendarService;
        _broadcaster = broadcaster;
        _logger = logger;
        _accessGuard = accessGuard;
        _frontendUrl = frontendOptions.Value.TrimmedUrl;
    }

    public async Task<IReadOnlyList<PlannedBlockDto>> GetBlocksAsync(Guid projectId, Guid userId, DateOnly date)
    {
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer);

        var blocks = await _dbContext.PlannedBlocks
            .AsNoTracking()
            .Include(x => x.Card)
            .Where(x => x.ProjectId == projectId && x.UserId == userId && x.Date == date)
            .OrderBy(x => x.StartTime)
            .Select(x => new PlannedBlockDto(
                x.Id,
                x.CardId,
                x.Card.Title,
                x.ProjectId,
                x.Date,
                x.StartTime,
                x.EndTime,
                x.SyncStatus,
                x.GoogleEventId))
            .ToListAsync();

        return blocks;
    }

    public async Task<PlannedBlockDto> CreateBlockAsync(Guid projectId, Guid userId, CreatePlannedBlockRequest request)
    {
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Member);

        var card = await _dbContext.Cards
            .AsNoTracking()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .FirstOrDefaultAsync(x => x.Id == request.CardId);

        if (card is null)
        {
            throw new NotFoundException("Card not found.");
        }

        if (card.Column.Board.ProjectId != projectId)
        {
            throw new BadRequestException("Card does not belong to this project.");
        }

        var now = DateTime.UtcNow;
        var block = new PlannedBlock
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CardId = request.CardId,
            ProjectId = projectId,
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            SyncStatus = PlannedBlockSyncStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.PlannedBlocks.Add(block);
        await _dbContext.SaveChangesAsync();

        await TrySyncCreateAsync(block, card.Title, card.Column.BoardId, request.TimeZone);

        await _broadcaster.PlannerBlockChanged(projectId, userId);

        return new PlannedBlockDto(
            block.Id,
            block.CardId,
            card.Title,
            block.ProjectId,
            block.Date,
            block.StartTime,
            block.EndTime,
            block.SyncStatus,
            block.GoogleEventId);
    }

    public async Task<PlannedBlockDto> UpdateBlockAsync(Guid blockId, Guid userId, UpdatePlannedBlockRequest request)
    {
        var block = await _dbContext.PlannedBlocks
            .Include(x => x.Card)
                .ThenInclude(x => x.Column)
            .FirstOrDefaultAsync(x => x.Id == blockId);

        if (block is null)
        {
            throw new NotFoundException("Planned block not found.");
        }

        if (block.UserId != userId)
        {
            throw new ForbiddenException("Forbidden.");
        }

        if (request.Date.HasValue)
        {
            block.Date = request.Date.Value;
        }

        if (request.StartTime.HasValue)
        {
            block.StartTime = request.StartTime.Value;
        }

        if (request.EndTime.HasValue)
        {
            block.EndTime = request.EndTime.Value;
        }

        if (block.EndTime <= block.StartTime)
        {
            throw new BadRequestException("End time must be after start time.");
        }

        block.SyncStatus = PlannedBlockSyncStatus.Pending;
        block.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await TrySyncUpdateAsync(block, block.Card.Title, block.Card.Column.BoardId, request.TimeZone);

        await _broadcaster.PlannerBlockChanged(block.ProjectId, userId);

        return new PlannedBlockDto(
            block.Id,
            block.CardId,
            block.Card.Title,
            block.ProjectId,
            block.Date,
            block.StartTime,
            block.EndTime,
            block.SyncStatus,
            block.GoogleEventId);
    }

    public async Task DeleteBlockAsync(Guid blockId, Guid userId)
    {
        var block = await _dbContext.PlannedBlocks
            .FirstOrDefaultAsync(x => x.Id == blockId);

        if (block is null)
        {
            throw new NotFoundException("Planned block not found.");
        }

        if (block.UserId != userId)
        {
            throw new ForbiddenException("Forbidden.");
        }

        var googleEventId = block.GoogleEventId;
        var blockUserId = block.UserId;
        var projectId = block.ProjectId;

        _dbContext.PlannedBlocks.Remove(block);
        await _dbContext.SaveChangesAsync();

        if (!string.IsNullOrEmpty(googleEventId))
        {
            try
            {
                await _calendarService.DeleteEventAsync(blockUserId, googleEventId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete Google Calendar event {EventId} for user {UserId}", googleEventId, blockUserId);
            }
        }

        await _broadcaster.PlannerBlockChanged(projectId, blockUserId);
    }

    public async Task<IReadOnlyList<UnscheduledCardDto>> GetUnscheduledCardsAsync(Guid projectId, Guid userId, DateOnly date)
    {
        await _accessGuard.RequireAccessAsync(projectId, userId, ProjectRole.Viewer);

        var scheduledCardIds = await _dbContext.PlannedBlocks
            .Where(x => x.UserId == userId && x.ProjectId == projectId && x.Date == date)
            .Select(x => x.CardId)
            .ToListAsync();

        var cards = await _dbContext.Cards
            .AsNoTracking()
            .Include(x => x.Column)
                .ThenInclude(x => x.Board)
            .Where(x => x.Column.Board.ProjectId == projectId && !scheduledCardIds.Contains(x.Id))
            .OrderBy(x => x.Column.Board.Position)
            .ThenBy(x => x.Column.Position)
            .ThenBy(x => x.Position)
            .Select(x => new UnscheduledCardDto(
                x.Id,
                x.Title,
                x.Description,
                x.ColumnId,
                x.Column.Name,
                x.Column.BoardId,
                x.Column.Board.Name))
            .ToListAsync();

        return cards;
    }

    private async Task TrySyncCreateAsync(PlannedBlock block, string cardTitle, Guid boardId, string? timeZone = null)
    {
        try
        {
            var tz = timeZone ?? "UTC";
            var start = block.Date.ToDateTime(block.StartTime, DateTimeKind.Unspecified);
            var end = block.Date.ToDateTime(block.EndTime, DateTimeKind.Unspecified);
            var cardUrl = $"{_frontendUrl}/projects/{block.ProjectId}/boards/{boardId}?cardId={block.CardId}";

            var eventId = await _calendarService.CreateEventAsync(
                block.UserId, cardTitle, cardUrl, start, end, tz);

            block.GoogleEventId = eventId;
            block.SyncStatus = PlannedBlockSyncStatus.Synced;
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync planned block {BlockId} to Google Calendar for user {UserId}", block.Id, block.UserId);
            block.SyncStatus = PlannedBlockSyncStatus.Failed;
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task TrySyncUpdateAsync(PlannedBlock block, string cardTitle, Guid boardId, string? timeZone = null)
    {
        if (string.IsNullOrEmpty(block.GoogleEventId))
        {
            await TrySyncCreateAsync(block, cardTitle, boardId, timeZone);
            return;
        }

        try
        {
            var tz = timeZone ?? "UTC";
            var start = block.Date.ToDateTime(block.StartTime, DateTimeKind.Unspecified);
            var end = block.Date.ToDateTime(block.EndTime, DateTimeKind.Unspecified);

            await _calendarService.UpdateEventAsync(
                block.UserId, block.GoogleEventId, cardTitle, start, end, tz);

            block.SyncStatus = PlannedBlockSyncStatus.Synced;
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Google Calendar event {EventId} for user {UserId}", block.GoogleEventId, block.UserId);
            block.SyncStatus = PlannedBlockSyncStatus.Failed;
            await _dbContext.SaveChangesAsync();
        }
    }
}
