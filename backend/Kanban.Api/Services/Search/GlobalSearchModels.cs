namespace Kanban.Api.Services.Search;

public sealed record GlobalSearchResult(
    IReadOnlyList<GlobalSearchItem> Items);

public sealed record GlobalSearchItem(
    Guid Id,
    string Type,
    string Name,
    string? Description,
    GlobalSearchItemLocation? Location);

public sealed record GlobalSearchItemLocation(
    Guid? ProjectId,
    string? ProjectName,
    Guid? BoardId,
    string? BoardName,
    Guid? ColumnId,
    string? ColumnName);
