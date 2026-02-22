namespace Kanban.Api.Services.Boards;

public sealed record CreateBoardRequest(string Name);

public sealed record UpdateBoardRequest(string Name, int Position);

public sealed record UpdateBoardDto(string Name, int Position);
