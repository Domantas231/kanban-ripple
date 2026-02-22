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

public sealed class SwimlaneView
{
    public Guid ProjectId { get; set; }
    public List<BoardSwimlane> Boards { get; set; } = new();
}

public sealed class BoardSwimlane
{
    public Board Board { get; set; } = null!;
    public List<ColumnSwimlane> Columns { get; set; } = new();
}

public sealed class ColumnSwimlane
{
    public Column Column { get; set; } = null!;
    public List<Card> Cards { get; set; } = new();
    public int CardCount { get; set; }
}
