namespace Kanban.Api.Services.Columns;

public sealed record CreateColumnRequest(string Name);

public sealed record UpdateColumnRequest(string Name);

public sealed record ReorderColumnRequest(Guid? BeforeColumnId, Guid? AfterColumnId);

public sealed record UpdateColumnDto(string Name);

public sealed record ReorderColumnDto(Guid? BeforeColumnId, Guid? AfterColumnId);
