namespace Kanban.Api.Services.Comments;

public sealed record CreateCommentRequest(string Content);

public sealed record UpdateCommentRequest(string Content);

public sealed record CreateCommentDto(string Content);

public sealed record UpdateCommentDto(string Content);
