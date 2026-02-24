namespace Kanban.Api.Services.Tags;

public sealed record CreateTagRequest(string Name, string Color);

public sealed record UpdateTagRequest(string? Name, string? Color);

public sealed record CreateTagDto(string Name, string Color);

public sealed record UpdateTagDto(string? Name, string? Color);
