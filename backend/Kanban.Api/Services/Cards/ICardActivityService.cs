using Kanban.Api.Models;

namespace Kanban.Api.Services.Cards;

public interface ICardActivityService
{
    Task LogAsync(Guid cardId, Guid userId, ActivityAction action, string? field = null, string? oldValue = null, string? newValue = null);
    Task<List<CardActivity>> ListByCardAsync(Guid cardId, Guid userId);
    Task<List<ProjectActivityDto>> ListByProjectAsync(Guid projectId, Guid userId, int limit = 30);
}

public sealed class ProjectActivityDto
{
    public Guid Id { get; init; }
    public string EntityType { get; init; } = "card";
    public Guid? CardId { get; init; }
    public string? CardTitle { get; init; }
    public Guid? BoardId { get; init; }
    public string? BoardName { get; init; }
    public string? ColumnName { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public ActivityAction Action { get; init; }
    public string? Field { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public DateTime CreatedAt { get; init; }
    public string EntityName { get; init; } = string.Empty;
}
