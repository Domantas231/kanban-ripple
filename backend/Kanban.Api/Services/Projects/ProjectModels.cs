using Kanban.Api.Models;

namespace Kanban.Api.Services.Projects;

public sealed record UpdateProjectDto(string Name, ProjectType Type);

public sealed record ProjectMemberDto(
    Guid UserId,
    string Email,
    ProjectRole Role,
    DateTime JoinedAt);

public sealed record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
