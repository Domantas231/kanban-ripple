namespace Kanban.Api.Services.Cards;

public sealed record CreateCardRequest(string Title, string? Description, decimal? PlannedDurationHours);

public sealed record UpdateCardRequest(string Title, string? Description, decimal? PlannedDurationHours, int Version);

public sealed record MoveCardRequest(Guid ColumnId, int Position);

public sealed record CreateCardDto(string Title, string? Description, decimal? PlannedDurationHours);

public sealed record UpdateCardDto(string Title, string? Description, decimal? PlannedDurationHours, int Version);

public sealed record MoveCardDto(Guid ColumnId, int Position);
