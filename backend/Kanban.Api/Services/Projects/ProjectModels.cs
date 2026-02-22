using Kanban.Api.Models;

namespace Kanban.Api.Services.Projects;

public sealed record UpdateProjectDto(string Name, ProjectType Type);

public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
