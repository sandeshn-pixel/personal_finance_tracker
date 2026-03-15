using FinanceTracker.Application.Goals.DTOs;

namespace FinanceTracker.Application.Goals.Interfaces;

public interface IGoalService
{
    Task<IReadOnlyCollection<GoalDto>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<GoalDetailsDto?> GetAsync(Guid userId, Guid goalId, CancellationToken cancellationToken);
    Task<GoalDto> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken cancellationToken);
    Task<GoalDto> UpdateAsync(Guid userId, Guid goalId, UpdateGoalRequest request, CancellationToken cancellationToken);
    Task<GoalDetailsDto> RecordContributionAsync(Guid userId, Guid goalId, RecordGoalEntryRequest request, CancellationToken cancellationToken);
    Task<GoalDetailsDto> RecordWithdrawalAsync(Guid userId, Guid goalId, RecordGoalEntryRequest request, CancellationToken cancellationToken);
    Task<GoalDto> MarkCompletedAsync(Guid userId, Guid goalId, CancellationToken cancellationToken);
    Task<GoalDto> ArchiveAsync(Guid userId, Guid goalId, CancellationToken cancellationToken);
}