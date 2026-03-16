using FinanceTracker.Application.Common;
using FinanceTracker.Application.Goals.DTOs;
using FinanceTracker.Application.Goals.Interfaces;
using FinanceTracker.Application.Notifications.DTOs;
using FinanceTracker.Application.Notifications.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class GoalService(ApplicationDbContext dbContext, INotificationService notificationService) : IGoalService
{
    public async Task<IReadOnlyCollection<GoalDto>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var goals = await dbContext.Goals
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Include(x => x.LinkedAccount)
            .OrderBy(x => x.Status)
            .ThenBy(x => x.TargetDateUtc)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return goals.Select(MapGoal).ToList();
    }

    public async Task<GoalDetailsDto?> GetAsync(Guid userId, Guid goalId, CancellationToken cancellationToken)
    {
        var goal = await dbContext.Goals
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Id == goalId)
            .Include(x => x.LinkedAccount)
            .SingleOrDefaultAsync(cancellationToken);

        if (goal is null)
        {
            return null;
        }

        var entries = await dbContext.GoalEntries
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.GoalId == goalId)
            .Include(x => x.Account)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.CreatedUtc)
            .Select(x => new GoalEntryDto(
                x.Id,
                x.Type,
                x.Amount,
                x.GoalAmountAfterEntry,
                x.OccurredAtUtc,
                x.Note,
                x.AccountId,
                x.Account != null ? x.Account.Name : null,
                x.CreatedUtc))
            .ToListAsync(cancellationToken);

        return new GoalDetailsDto(MapGoal(goal), entries);
    }

    public async Task<GoalDto> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var linkedAccount = await ResolveLinkedAccountAsync(userId, request.LinkedAccountId, cancellationToken);

        var goal = new Goal
        {
            UserId = userId,
            Name = request.Name.Trim(),
            TargetAmount = RoundMoney(request.TargetAmount),
            CurrentAmount = 0m,
            TargetDateUtc = request.TargetDateUtc.HasValue ? DateTime.SpecifyKind(request.TargetDateUtc.Value, DateTimeKind.Utc) : null,
            LinkedAccountId = linkedAccount?.Id,
            Icon = NormalizeNullable(request.Icon),
            Color = NormalizeNullable(request.Color),
            Status = GoalStatus.Active
        };

        dbContext.Goals.Add(goal);
        await dbContext.SaveChangesAsync(cancellationToken);
        goal.LinkedAccount = linkedAccount;
        return MapGoal(goal);
    }

    public async Task<GoalDto> UpdateAsync(Guid userId, Guid goalId, UpdateGoalRequest request, CancellationToken cancellationToken)
    {
        var goal = await dbContext.Goals
            .Include(x => x.LinkedAccount)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == goalId, cancellationToken)
            ?? throw new NotFoundException("Goal was not found.");

        if (goal.Status == GoalStatus.Archived)
        {
            throw new ValidationException("Archived goals cannot be edited.");
        }

        var linkedAccount = await ResolveLinkedAccountAsync(userId, request.LinkedAccountId, cancellationToken);

        goal.Name = request.Name.Trim();
        goal.TargetAmount = RoundMoney(request.TargetAmount);
        goal.TargetDateUtc = request.TargetDateUtc.HasValue ? DateTime.SpecifyKind(request.TargetDateUtc.Value, DateTimeKind.Utc) : null;
        goal.LinkedAccountId = linkedAccount?.Id;
        goal.LinkedAccount = linkedAccount;
        goal.Icon = NormalizeNullable(request.Icon);
        goal.Color = NormalizeNullable(request.Color);
        goal.Status = ResolveGoalStatus(goal.Status, goal.CurrentAmount, goal.TargetAmount);

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapGoal(goal);
    }

    public Task<GoalDetailsDto> RecordContributionAsync(Guid userId, Guid goalId, RecordGoalEntryRequest request, CancellationToken cancellationToken)
        => RecordEntryAsync(userId, goalId, request, GoalEntryType.Contribution, cancellationToken);

    public Task<GoalDetailsDto> RecordWithdrawalAsync(Guid userId, Guid goalId, RecordGoalEntryRequest request, CancellationToken cancellationToken)
        => RecordEntryAsync(userId, goalId, request, GoalEntryType.Withdrawal, cancellationToken);

    public async Task<GoalDto> MarkCompletedAsync(Guid userId, Guid goalId, CancellationToken cancellationToken)
    {
        var goal = await dbContext.Goals
            .Include(x => x.LinkedAccount)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == goalId, cancellationToken)
            ?? throw new NotFoundException("Goal was not found.");

        if (goal.Status == GoalStatus.Archived)
        {
            throw new ValidationException("Archived goals cannot be completed.");
        }

        if (goal.CurrentAmount < goal.TargetAmount)
        {
            throw new ValidationException("Goal can only be marked completed after reaching the target amount.");
        }

        goal.Status = GoalStatus.Completed;
        await dbContext.SaveChangesAsync(cancellationToken);
        await PublishGoalCompletedNotificationAsync(goal, cancellationToken);
        return MapGoal(goal);
    }

    public async Task<GoalDto> ArchiveAsync(Guid userId, Guid goalId, CancellationToken cancellationToken)
    {
        var goal = await dbContext.Goals
            .Include(x => x.LinkedAccount)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == goalId, cancellationToken)
            ?? throw new NotFoundException("Goal was not found.");

        goal.Status = GoalStatus.Archived;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapGoal(goal);
    }

    private async Task<GoalDetailsDto> RecordEntryAsync(Guid userId, Guid goalId, RecordGoalEntryRequest request, GoalEntryType entryType, CancellationToken cancellationToken)
    {
        await using var transaction = await TransactionMapping.BeginFinancialTransactionAsync(dbContext, cancellationToken);

        var goal = await dbContext.Goals
            .Include(x => x.LinkedAccount)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == goalId, cancellationToken)
            ?? throw new NotFoundException("Goal was not found.");

        if (goal.Status == GoalStatus.Archived)
        {
            throw new ValidationException("Archived goals cannot receive contributions or withdrawals.");
        }

        var amount = RoundMoney(request.Amount);
        var account = goal.LinkedAccountId.HasValue
            ? await dbContext.Accounts.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == goal.LinkedAccountId.Value && !x.IsArchived, cancellationToken)
            : null;

        if (goal.LinkedAccountId.HasValue && account is null)
        {
            throw new ValidationException("Linked account is no longer available for this goal.");
        }

        if (entryType == GoalEntryType.Withdrawal && goal.CurrentAmount < amount)
        {
            throw new ValidationException("Withdrawal amount exceeds the goal's current balance.");
        }

        if (entryType == GoalEntryType.Contribution)
        {
            goal.CurrentAmount = RoundMoney(goal.CurrentAmount + amount);
            if (account is not null)
            {
                account.CurrentBalance -= amount;
            }
        }
        else
        {
            goal.CurrentAmount = RoundMoney(goal.CurrentAmount - amount);
            if (account is not null)
            {
                account.CurrentBalance += amount;
            }
        }

        var previousStatus = goal.Status;
        goal.Status = ResolveGoalStatus(goal.Status, goal.CurrentAmount, goal.TargetAmount);

        var entry = new GoalEntry
        {
            GoalId = goal.Id,
            UserId = userId,
            AccountId = account?.Id,
            Type = entryType,
            Amount = amount,
            GoalAmountAfterEntry = goal.CurrentAmount,
            Note = NormalizeNullable(request.Note),
            OccurredAtUtc = request.OccurredAtUtc.HasValue ? DateTime.SpecifyKind(request.OccurredAtUtc.Value, DateTimeKind.Utc) : DateTime.UtcNow
        };

        dbContext.GoalEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (previousStatus != GoalStatus.Completed && goal.Status == GoalStatus.Completed)
        {
            await PublishGoalCompletedNotificationAsync(goal, cancellationToken);
        }

        return await GetAsync(userId, goal.Id, cancellationToken) ?? throw new NotFoundException("Goal was not found after recording the entry.");
    }

    private async Task PublishGoalCompletedNotificationAsync(Goal goal, CancellationToken cancellationToken)
    {
        await notificationService.PublishAsync(new PublishNotificationRequest(
            goal.UserId,
            NotificationType.GoalCompleted,
            NotificationLevel.Success,
            $"Goal completed: {goal.Name}",
            $"{goal.Name} reached its target amount of {goal.TargetAmount:0.00}.",
            "/goals",
            $"goal-completed:{goal.Id}"), cancellationToken);
    }

    private async Task<Account?> ResolveLinkedAccountAsync(Guid userId, Guid? linkedAccountId, CancellationToken cancellationToken)
    {
        if (!linkedAccountId.HasValue)
        {
            return null;
        }

        return await dbContext.Accounts.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == linkedAccountId.Value && !x.IsArchived, cancellationToken)
            ?? throw new ValidationException("Linked account is invalid or archived.");
    }

    private static GoalStatus ResolveGoalStatus(GoalStatus currentStatus, decimal currentAmount, decimal targetAmount)
    {
        if (currentStatus == GoalStatus.Archived)
        {
            return GoalStatus.Archived;
        }

        return currentAmount >= targetAmount ? GoalStatus.Completed : GoalStatus.Active;
    }

    private static GoalDto MapGoal(Goal goal)
    {
        var remaining = decimal.Round(Math.Max(goal.TargetAmount - goal.CurrentAmount, 0m), 2, MidpointRounding.AwayFromZero);
        var progress = goal.TargetAmount == 0m ? 0m : decimal.Round((goal.CurrentAmount / goal.TargetAmount) * 100m, 2, MidpointRounding.AwayFromZero);
        return new GoalDto(
            goal.Id,
            goal.Name,
            goal.TargetAmount,
            goal.CurrentAmount,
            remaining,
            progress,
            goal.TargetDateUtc,
            goal.LinkedAccountId,
            goal.LinkedAccount?.Name,
            goal.Icon,
            goal.Color,
            goal.Status,
            goal.CreatedUtc,
            goal.UpdatedUtc);
    }

    private static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}