using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Infrastructure.Financial;

internal static class RecurringScheduleCalculator
{
    public static DateTime NormalizeDate(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    public static DateTime CalculateNextOccurrence(RecurringFrequency frequency, DateTime currentDateUtc)
        => frequency switch
        {
            RecurringFrequency.Daily => currentDateUtc.AddDays(1),
            RecurringFrequency.Weekly => currentDateUtc.AddDays(7),
            RecurringFrequency.Monthly => currentDateUtc.AddMonths(1),
            RecurringFrequency.Yearly => currentDateUtc.AddYears(1),
            _ => throw new ValidationException("Unsupported recurring frequency.")
        };

    public static DateTime? CalculateInitialNextRun(DateTime startDateUtc, DateTime? endDateUtc)
    {
        if (endDateUtc.HasValue && startDateUtc > endDateUtc.Value)
        {
            return null;
        }

        return startDateUtc;
    }

    public static DateTime? RecalculateNextRunDate(RecurringTransactionRule rule, IEnumerable<RecurringTransactionExecution> executions)
    {
        var nextRun = rule.StartDateUtc;
        var lastProcessed = executions
            .Where(x => x.Status is RecurringExecutionStatus.Completed or RecurringExecutionStatus.Reminded)
            .OrderByDescending(x => x.ScheduledForDateUtc)
            .Select(x => (DateTime?)x.ScheduledForDateUtc)
            .FirstOrDefault();

        if (lastProcessed.HasValue)
        {
            nextRun = CalculateNextOccurrence(rule.Frequency, lastProcessed.Value);
        }

        if (rule.EndDateUtc.HasValue && nextRun > NormalizeDate(rule.EndDateUtc.Value))
        {
            return null;
        }

        return nextRun;
    }

    public static void AdvanceRule(RecurringTransactionRule rule, DateTime processedDate)
    {
        var nextRun = CalculateNextOccurrence(rule.Frequency, processedDate);
        if (rule.EndDateUtc.HasValue && nextRun > NormalizeDate(rule.EndDateUtc.Value))
        {
            rule.NextRunDateUtc = null;
            rule.Status = RecurringRuleStatus.Completed;
        }
        else
        {
            rule.NextRunDateUtc = nextRun;
            rule.Status = RecurringRuleStatus.Active;
        }
    }
}