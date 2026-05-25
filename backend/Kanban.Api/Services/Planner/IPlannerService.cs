namespace Kanban.Api.Services.Planner;

public interface IPlannerService
{
    Task<IReadOnlyList<PlannedBlockDto>> GetBlocksAsync(Guid projectId, Guid userId, DateOnly date);
    Task<PlannedBlockDto> CreateBlockAsync(Guid projectId, Guid userId, CreatePlannedBlockRequest request);
    Task<PlannedBlockDto> UpdateBlockAsync(Guid blockId, Guid userId, UpdatePlannedBlockRequest request);
    Task DeleteBlockAsync(Guid blockId, Guid userId);
    Task<IReadOnlyList<UnscheduledCardDto>> GetUnscheduledCardsAsync(Guid projectId, Guid userId, DateOnly date);
}
