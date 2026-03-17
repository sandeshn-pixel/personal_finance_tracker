using FinanceTracker.Application.Automation.DTOs;
using FinanceTracker.Application.Automation.Interfaces;
using FinanceTracker.Application.Notifications.DTOs;
using FinanceTracker.Application.Notifications.Interfaces;
using FinanceTracker.Application.RecurringTransactions.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Automation;

public sealed class AutomationService(
    ApplicationDbContext dbContext,
    IRecurringTransactionService recurringTransactionService,
    INotificationService notificationService,
    IOptions<AutomationOptions> automationOptions,
    ILogger<AutomationService> logger) : IAutomationService
{
    public async Task<AutomationRunSummaryDto> RunAsync(DateTime asOfUtc, CancellationToken cancellationToken)
    {
        var normalizedAsOf = RecurringScheduleCalculator.NormalizeDate(asOfUtc);
        var usersProcessed = 0;
        var transactionsCreated = 0;
        var autoOccurrencesProcessed = 0;

        var autoUsers = await dbContext.RecurringTransactionRules
            .AsNoTracking()
            .Where(x => x.Status == RecurringRuleStatus.Active && x.AutoCreateTransaction && x.NextRunDateUtc != null && x.NextRunDateUtc <= normalizedAsOf)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var userId in autoUsers)
        {
            var summary = await recurringTransactionService.ProcessDueAsync(userId, normalizedAsOf, cancellationToken);
            usersProcessed++;
            transactionsCreated += summary.TransactionsCreated;
            autoOccurrencesProcessed += summary.OccurrencesProcessed;
        }

        var manualRemindersCreated = await CreateManualRecurringRemindersAsync(normalizedAsOf, cancellationToken);
        var goalRemindersCreated = await CreateGoalRemindersAsync(normalizedAsOf, cancellationToken);

        if (transactionsCreated > 0 || manualRemindersCreated > 0 || goalRemindersCreated > 0)
        {
            logger.LogInformation(
                "Automation cycle completed at {ProcessedAtUtc}. Users processed: {UsersProcessed}, transactions created: {TransactionsCreated}, manual reminders: {ManualRemindersCreated}, goal reminders: {GoalRemindersCreated}",
                DateTime.UtcNow,
                usersProcessed,
                transactionsCreated,
                manualRemindersCreated,
                goalRemindersCreated);
        }

        return new AutomationRunSummaryDto(usersProcessed, transactionsCreated, autoOccurrencesProcessed, manualRemindersCreated, goalRemindersCreated, DateTime.UtcNow);
    }

    private async Task<int> CreateManualRecurringRemindersAsync(DateTime asOfUtc, CancellationToken cancellationToken)
    {
        var remindersCreated = 0;
        var dueRules = await dbContext.RecurringTransactionRules
            .Where(x => x.Status == RecurringRuleStatus.Active && !x.AutoCreateTransaction && x.NextRunDateUtc != null && x.NextRunDateUtc <= asOfUtc)
            .Include(x => x.Executions)
            .ToListAsync(cancellationToken);

        foreach (var rule in dueRules)
        {
            var scheduledDate = RecurringScheduleCalculator.NormalizeDate(rule.NextRunDateUtc!.Value);
            if (rule.Executions.Any(x => x.ScheduledForDateUtc == scheduledDate && x.Status == RecurringExecutionStatus.Reminded))
            {
                continue;
            }

            await using var transaction = await TransactionMapping.BeginFinancialTransactionAsync(dbContext, cancellationToken);
            var execution = new RecurringTransactionExecution
            {
                RecurringTransactionRuleId = rule.Id,
                ScheduledForDateUtc = scheduledDate,
                Status = RecurringExecutionStatus.Reminded,
                ProcessedAtUtc = DateTime.UtcNow,
                FailureReason = "Manual reminder issued."
            };

            dbContext.RecurringTransactionExecutions.Add(execution);
            var route = $"/recurring";
            var published = await notificationService.PublishAsync(new PublishNotificationRequest(
                rule.UserId,
                NotificationType.RecurringDueReminder,
                NotificationLevel.Info,
                $"Recurring reminder: {rule.Title}",
                $"Review {rule.Title}. It is due on {scheduledDate:dd MMM yyyy} as a {rule.Frequency.ToString().ToLowerInvariant()} {rule.Type.ToString().ToLowerInvariant()} rule.",
                route,
                $"recurring-due:{rule.Id}:{scheduledDate:yyyyMMdd}"), cancellationToken);

            RecurringScheduleCalculator.AdvanceRule(rule, scheduledDate);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (published)
            {
                remindersCreated++;
            }
        }

        return remindersCreated;
    }

    private async Task<int> CreateGoalRemindersAsync(DateTime asOfUtc, CancellationToken cancellationToken)
    {
        var options = automationOptions.Value;
        var startDate = asOfUtc.Date;
        var endDate = startDate.AddDays(Math.Max(options.GoalReminderLookaheadDays, 1));
        var goals = await dbContext.Goals
            .AsNoTracking()
            .Where(x => x.Status == GoalStatus.Active && x.TargetDateUtc != null && x.TargetDateUtc >= startDate && x.TargetDateUtc <= endDate)
            .ToListAsync(cancellationToken);

        var remindersCreated = 0;
        foreach (var goal in goals)
        {
            var remainingDays = (goal.TargetDateUtc!.Value.Date - startDate).Days;
            var published = await notificationService.PublishAsync(new PublishNotificationRequest(
                goal.UserId,
                NotificationType.GoalTargetApproaching,
                NotificationLevel.Warning,
                $"Goal reminder: {goal.Name}",
                remainingDays == 0
                    ? $"{goal.Name} reaches its target date today. Check progress and decide whether another contribution is needed."
                    : $"{goal.Name} reaches its target date in {remainingDays} day{(remainingDays == 1 ? string.Empty : "s")}. Review progress before the deadline.",
                "/goals",
                $"goal-target-approaching:{goal.Id}:{goal.TargetDateUtc:yyyyMMdd}"), cancellationToken);

            if (published)
            {
                remindersCreated++;
            }
        }

        return remindersCreated;
    }
}
