using Kanban.Api.Models;

namespace Kanban.Api.Services.Planner;

public sealed record PlannedBlockDto(
    Guid Id,
    Guid CardId,
    string CardTitle,
    Guid ProjectId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    PlannedBlockSyncStatus SyncStatus,
    string? GoogleEventId);

public sealed record CreatePlannedBlockRequest(
    Guid CardId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? TimeZone = null);

public sealed record UpdatePlannedBlockRequest(
    DateOnly? Date,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string? TimeZone = null);

public sealed record UnscheduledCardDto(
    Guid Id,
    string Title,
    string? Description,
    Guid ColumnId,
    string ColumnName,
    Guid BoardId,
    string BoardName);
