namespace Kanban.Api.Services.Cards;

public sealed record CreateCardRequest(string Title, string? Description, DateTime? StartDate, DateTime? DueDate, double? EstimatedHours);

public sealed record UpdateCardRequest(string Title, string? Description, DateTime? StartDate, DateTime? DueDate, double? EstimatedHours, int Version);

public sealed record ScheduleCardRequest(DateTime? StartDate, DateTime? DueDate);

public sealed record MoveCardRequest(Guid ColumnId, int Position);

public sealed record CreateCardDto(string Title, string? Description, DateTime? StartDate, DateTime? DueDate, double? EstimatedHours);

public sealed record UpdateCardDto(string Title, string? Description, DateTime? StartDate, DateTime? DueDate, double? EstimatedHours, int Version);

public sealed record ScheduleCardDto(DateTime? StartDate, DateTime? DueDate);

public sealed record MoveCardDto(Guid ColumnId, int Position);

public sealed record FilterCriteria(
	IReadOnlyCollection<Guid>? TagIds,
	IReadOnlyCollection<Guid>? UserIds,
	IReadOnlyCollection<Guid>? ColumnIds);

public sealed record CreateSubtaskRequest(string Description, bool? Completed);

public sealed record UpdateSubtaskRequest(string? Description, bool? Completed, int? Position);

public sealed record CreateSubtaskDto(string Description, bool? Completed);

public sealed record UpdateSubtaskDto(string? Description, bool? Completed, int? Position);

public sealed record SubtaskCountsDto(int Completed, int Total);
