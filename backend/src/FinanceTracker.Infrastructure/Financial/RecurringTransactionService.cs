using FinanceTracker.Application.Common;
using FinanceTracker.Application.Notifications.DTOs;
using FinanceTracker.Application.Notifications.Interfaces;
using FinanceTracker.Application.RecurringTransactions.DTOs;
using FinanceTracker.Application.RecurringTransactions.Interfaces;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Application.Transactions.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Automation;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class RecurringTransactionService(
    ApplicationDbContext dbContext,
    ITransactionService transactionService,
    INotificationService notificationService,
    IOptions<AutomationOptions> automationOptions,
    AccountAccessService accountAccessService) : IRecurringTransactionService
{
    public async Task<IReadOnlyCollection<RecurringTransactionDto>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var accessibleAccountIds = await accountAccessService.QueryAccessibleAccounts(userId, AccountMemberRole.Viewer, includeArchived: true)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var rules = await dbContext.RecurringTransactionRules
            .AsNoTracking()
            .Where(x => x.Status != RecurringRuleStatus.Deleted
                && (x.UserId == userId
                    || accessibleAccountIds.Contains(x.AccountId)
                    || (x.TransferAccountId.HasValue && accessibleAccountIds.Contains(x.TransferAccountId.Value))))
            .Include(x => x.Account)
            .Include(x => x.TransferAccount)
            .Include(x => x.Category)
            .Include(x => x.Executions)
            .OrderBy(x => x.Status)
            .ThenBy(x => x.NextRunDateUtc)
            .ThenBy(x => x.Title)
            .ToListAsync(cancellationToken);

        return rules.Select(rule => MapRule(rule, rule.UserId == userId)).ToList();
    }

    public async Task<RecurringTransactionDto?> GetAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken)
    {
        var accessibleAccountIds = await accountAccessService.QueryAccessibleAccounts(userId, AccountMemberRole.Viewer, includeArchived: true)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var rule = await dbContext.RecurringTransactionRules
            .AsNoTracking()
            .Where(x => x.Id == ruleId
                && x.Status != RecurringRuleStatus.Deleted
                && (x.UserId == userId
                    || accessibleAccountIds.Contains(x.AccountId)
                    || (x.TransferAccountId.HasValue && accessibleAccountIds.Contains(x.TransferAccountId.Value))))
            .Include(x => x.Account)
            .Include(x => x.TransferAccount)
            .Include(x => x.Category)
            .Include(x => x.Executions)
            .SingleOrDefaultAsync(cancellationToken);

        return rule is null ? null : MapRule(rule, rule.UserId == userId);
    }

    public async Task<RecurringTransactionDto> CreateAsync(Guid userId, CreateRecurringTransactionRequest request, CancellationToken cancellationToken)
    {
        var resolved = await ResolveReferencesAsync(userId, request.Type, request.AccountId, request.TransferAccountId, request.CategoryId, cancellationToken);
        var startDate = RecurringScheduleCalculator.NormalizeDate(request.StartDateUtc);
        DateTime? endDate = request.EndDateUtc.HasValue ? RecurringScheduleCalculator.NormalizeDate(request.EndDateUtc.Value) : null;

        var rule = new RecurringTransactionRule
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Type = request.Type,
            Amount = RoundMoney(request.Amount),
            CategoryId = resolved.Category?.Id,
            AccountId = resolved.SourceAccount.Id,
            TransferAccountId = resolved.TransferAccount?.Id,
            Frequency = request.Frequency,
            StartDateUtc = startDate,
            EndDateUtc = endDate,
            NextRunDateUtc = RecurringScheduleCalculator.CalculateInitialNextRun(startDate, endDate),
            AutoCreateTransaction = request.AutoCreateTransaction,
            Status = endDate.HasValue && startDate > endDate.Value ? RecurringRuleStatus.Completed : RecurringRuleStatus.Active
        };

        dbContext.RecurringTransactionRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        rule.Account = resolved.SourceAccount;
        rule.TransferAccount = resolved.TransferAccount;
        rule.Category = resolved.Category;
        return MapRule(rule, true);
    }

    public async Task<RecurringTransactionDto> UpdateAsync(Guid userId, Guid ruleId, UpdateRecurringTransactionRequest request, CancellationToken cancellationToken)
    {
        var rule = await dbContext.RecurringTransactionRules
            .Include(x => x.Executions)
            .Include(x => x.Account)
            .Include(x => x.TransferAccount)
            .Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == ruleId && x.Status != RecurringRuleStatus.Deleted, cancellationToken)
            ?? throw new NotFoundException("Recurring rule was not found.");

        var resolved = await ResolveReferencesAsync(userId, request.Type, request.AccountId, request.TransferAccountId, request.CategoryId, cancellationToken);
        var startDate = RecurringScheduleCalculator.NormalizeDate(request.StartDateUtc);
        DateTime? endDate = request.EndDateUtc.HasValue ? RecurringScheduleCalculator.NormalizeDate(request.EndDateUtc.Value) : null;

        rule.Title = request.Title.Trim();
        rule.Type = request.Type;
        rule.Amount = RoundMoney(request.Amount);
        rule.CategoryId = resolved.Category?.Id;
        rule.Category = resolved.Category;
        rule.AccountId = resolved.SourceAccount.Id;
        rule.Account = resolved.SourceAccount;
        rule.TransferAccountId = resolved.TransferAccount?.Id;
        rule.TransferAccount = resolved.TransferAccount;
        rule.Frequency = request.Frequency;
        rule.StartDateUtc = startDate;
        rule.EndDateUtc = endDate;
        rule.AutoCreateTransaction = request.AutoCreateTransaction;

        if (rule.Status != RecurringRuleStatus.Deleted)
        {
            rule.NextRunDateUtc = RecurringScheduleCalculator.RecalculateNextRunDate(rule, rule.Executions);
            rule.Status = rule.NextRunDateUtc.HasValue
                ? (rule.Status == RecurringRuleStatus.Paused ? RecurringRuleStatus.Paused : RecurringRuleStatus.Active)
                : RecurringRuleStatus.Completed;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapRule(rule, true);
    }

    public async Task<RecurringTransactionDto> PauseAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken)
    {
        var rule = await dbContext.RecurringTransactionRules
            .Include(x => x.Account)
            .Include(x => x.TransferAccount)
            .Include(x => x.Category)
            .Include(x => x.Executions)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == ruleId && x.Status != RecurringRuleStatus.Deleted, cancellationToken)
            ?? throw new NotFoundException("Recurring rule was not found.");

        if (rule.Status == RecurringRuleStatus.Completed)
        {
            throw new ValidationException("Completed recurring rules cannot be paused.");
        }

        rule.Status = RecurringRuleStatus.Paused;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapRule(rule, true);
    }

    public async Task<RecurringTransactionDto> ResumeAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken)
    {
        var rule = await dbContext.RecurringTransactionRules
            .Include(x => x.Account)
            .Include(x => x.TransferAccount)
            .Include(x => x.Category)
            .Include(x => x.Executions)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == ruleId && x.Status != RecurringRuleStatus.Deleted, cancellationToken)
            ?? throw new NotFoundException("Recurring rule was not found.");

        if (rule.Status != RecurringRuleStatus.Paused)
        {
            throw new ValidationException("Only paused recurring rules can be resumed.");
        }

        rule.NextRunDateUtc = RecurringScheduleCalculator.RecalculateNextRunDate(rule, rule.Executions);
        rule.Status = rule.NextRunDateUtc.HasValue ? RecurringRuleStatus.Active : RecurringRuleStatus.Completed;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapRule(rule, true);
    }

    public async Task DeleteAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken)
    {
        var rule = await dbContext.RecurringTransactionRules
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == ruleId && x.Status != RecurringRuleStatus.Deleted, cancellationToken)
            ?? throw new NotFoundException("Recurring rule was not found.");

        rule.Status = RecurringRuleStatus.Deleted;
        rule.NextRunDateUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RecurringExecutionSummaryDto> ProcessDueAsync(Guid userId, DateTime asOfUtc, CancellationToken cancellationToken)
    {
        var normalizedAsOf = RecurringScheduleCalculator.NormalizeDate(asOfUtc);
        var rules = await dbContext.RecurringTransactionRules
            .Where(x => x.UserId == userId && x.Status == RecurringRuleStatus.Active && x.AutoCreateTransaction && x.NextRunDateUtc != null && x.NextRunDateUtc <= normalizedAsOf)
            .OrderBy(x => x.NextRunDateUtc)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var rulesVisited = 0;
        var transactionsCreated = 0;
        var occurrencesProcessed = 0;
        var occurrencesSkipped = 0;
        var occurrencesDeferredForRetry = 0;
        var occurrencesFailedPermanently = 0;

        foreach (var ruleId in rules)
        {
            rulesVisited++;
            var keepProcessing = true;
            while (keepProcessing)
            {
                var processed = await ProcessSingleOccurrenceAsync(userId, ruleId, normalizedAsOf, cancellationToken);
                transactionsCreated += processed.TransactionCreated ? 1 : 0;
                occurrencesProcessed += processed.OccurrenceProcessed ? 1 : 0;
                occurrencesSkipped += processed.OccurrenceSkipped ? 1 : 0;
                occurrencesDeferredForRetry += processed.OccurrenceDeferredForRetry ? 1 : 0;
                occurrencesFailedPermanently += processed.OccurrenceFailedPermanently ? 1 : 0;
                keepProcessing = processed.ShouldContinue;
            }
        }

        return new RecurringExecutionSummaryDto(
            rulesVisited,
            transactionsCreated,
            occurrencesProcessed,
            occurrencesSkipped,
            occurrencesDeferredForRetry,
            occurrencesFailedPermanently,
            DateTime.UtcNow);
    }

    private async Task<(bool ShouldContinue, bool TransactionCreated, bool OccurrenceProcessed, bool OccurrenceSkipped, bool OccurrenceDeferredForRetry, bool OccurrenceFailedPermanently)> ProcessSingleOccurrenceAsync(Guid userId, Guid ruleId, DateTime asOfUtc, CancellationToken cancellationToken)
    {
        var rule = await dbContext.RecurringTransactionRules
            .Include(x => x.Executions)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == ruleId, cancellationToken);

        if (rule is null || rule.Status != RecurringRuleStatus.Active || rule.NextRunDateUtc is null || rule.NextRunDateUtc > asOfUtc || !rule.AutoCreateTransaction)
        {
            return (false, false, false, false, false, false);
        }

        var options = automationOptions.Value;
        var scheduledDate = RecurringScheduleCalculator.NormalizeDate(rule.NextRunDateUtc.Value);
        var execution = rule.Executions.SingleOrDefault(x => x.ScheduledForDateUtc == scheduledDate);
        if (execution is not null)
        {
            var recovered = await TryRecoverExecutionAsync(userId, rule, execution, cancellationToken);
            if (recovered)
            {
                RecurringScheduleCalculator.AdvanceRule(rule, scheduledDate);
                await dbContext.SaveChangesAsync(cancellationToken);
                return (rule.NextRunDateUtc is not null && rule.NextRunDateUtc <= asOfUtc, false, true, false, false, false);
            }

            if (execution.Status == RecurringExecutionStatus.Failed && execution.AttemptCount >= options.MaxRecurringRetryAttempts)
            {
                return (false, false, false, true, false, true);
            }

            if (execution.NextRetryAfterUtc.HasValue && execution.NextRetryAfterUtc.Value > DateTime.UtcNow)
            {
                return (false, false, false, true, true, false);
            }

            if (execution.Status == RecurringExecutionStatus.Processing && execution.LastAttemptedUtc.HasValue && execution.LastAttemptedUtc.Value > DateTime.UtcNow.AddMinutes(-5))
            {
                return (false, false, false, true, false, false);
            }
        }

        var attemptStartedUtc = DateTime.UtcNow;
        if (execution is null)
        {
            execution = new RecurringTransactionExecution
            {
                RecurringTransactionRuleId = rule.Id,
                ScheduledForDateUtc = scheduledDate,
                Status = RecurringExecutionStatus.Processing,
                AttemptCount = 1,
                LastAttemptedUtc = attemptStartedUtc
            };
            dbContext.RecurringTransactionExecutions.Add(execution);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return (false, false, false, true, false, false);
            }
        }
        else
        {
            execution.Status = RecurringExecutionStatus.Processing;
            execution.AttemptCount += 1;
            execution.LastAttemptedUtc = attemptStartedUtc;
            execution.FailureReason = null;
            execution.NextRetryAfterUtc = null;
            execution.TransactionId = null;
            execution.ProcessedAtUtc = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        try
        {
            var createdTransaction = await transactionService.CreateAsync(userId, new UpsertTransactionRequest
            {
                AccountId = rule.AccountId,
                TransferAccountId = rule.TransferAccountId,
                Type = rule.Type,
                Amount = rule.Amount,
                DateUtc = scheduledDate,
                CategoryId = rule.CategoryId,
                Note = rule.Title,
                RecurringTransactionId = rule.Id,
                Tags = []
            }, cancellationToken);

            execution.Status = RecurringExecutionStatus.Completed;
            execution.TransactionId = createdTransaction.Id;
            execution.ProcessedAtUtc = DateTime.UtcNow;
            execution.FailureReason = null;
            execution.NextRetryAfterUtc = null;
            RecurringScheduleCalculator.AdvanceRule(rule, scheduledDate);
            await dbContext.SaveChangesAsync(cancellationToken);

            return (rule.NextRunDateUtc is not null && rule.NextRunDateUtc <= asOfUtc, true, true, false, false, false);
        }
        catch (ApplicationExceptionBase ex)
        {
            var failureRecordedUtc = DateTime.UtcNow;
            var permanentlyFailed = execution.AttemptCount >= options.MaxRecurringRetryAttempts;
            execution.Status = RecurringExecutionStatus.Failed;
            execution.FailureReason = ex.Message.Length > 280 ? ex.Message[..280] : ex.Message;
            execution.ProcessedAtUtc = failureRecordedUtc;
            execution.NextRetryAfterUtc = permanentlyFailed
                ? null
                : failureRecordedUtc.Add(ComputeRetryDelay(options, execution.AttemptCount));
            await dbContext.SaveChangesAsync(cancellationToken);

            if (execution.AttemptCount == 1 || permanentlyFailed)
            {
                var detail = permanentlyFailed
                    ? $"{rule.Title} could not create its scheduled transaction for {scheduledDate:dd MMM yyyy} after {execution.AttemptCount} attempts. Review the rule and fix the issue: {execution.FailureReason}"
                    : $"{rule.Title} could not create its scheduled transaction for {scheduledDate:dd MMM yyyy}. It will retry automatically around {execution.NextRetryAfterUtc:dd MMM yyyy HH:mm} UTC. Issue: {execution.FailureReason}";

                await notificationService.PublishAsync(new PublishNotificationRequest(
                    rule.UserId,
                    NotificationType.RecurringExecutionFailed,
                    NotificationLevel.Warning,
                    permanentlyFailed
                        ? $"Recurring transaction needs manual attention: {rule.Title}"
                        : $"Recurring transaction retry scheduled: {rule.Title}",
                    detail,
                    "/recurring",
                    permanentlyFailed
                        ? $"recurring-failed:{execution.Id}:exhausted"
                        : $"recurring-failed:{execution.Id}:first"), cancellationToken);
            }

            return (false, false, false, true, !permanentlyFailed, permanentlyFailed);
        }
    }

    private async Task<bool> TryRecoverExecutionAsync(Guid userId, RecurringTransactionRule rule, RecurringTransactionExecution execution, CancellationToken cancellationToken)
    {
        if (execution.Status == RecurringExecutionStatus.Completed)
        {
            return true;
        }

        var existingTransaction = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId
                && !x.IsDeleted
                && x.RecurringTransactionId == rule.Id
                && x.DateUtc == execution.ScheduledForDateUtc
                && x.AccountId == rule.AccountId
                && x.TransferAccountId == rule.TransferAccountId
                && x.Type == rule.Type
                && x.Amount == rule.Amount)
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingTransaction is null)
        {
            return false;
        }

        execution.Status = RecurringExecutionStatus.Completed;
        execution.TransactionId = existingTransaction.Id;
        execution.ProcessedAtUtc = DateTime.UtcNow;
        execution.FailureReason = null;
        execution.NextRetryAfterUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<(Account SourceAccount, Account? TransferAccount, Category? Category)> ResolveReferencesAsync(Guid userId, TransactionType type, Guid accountId, Guid? transferAccountId, Guid? categoryId, CancellationToken cancellationToken)
    {
        var sourceAccount = await dbContext.Accounts.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == accountId && !x.IsArchived, cancellationToken)
            ?? throw new ValidationException("Selected source account is invalid or archived.");

        Account? transferAccount = null;
        Category? category = null;

        if (type == TransactionType.Transfer)
        {
            if (!transferAccountId.HasValue)
            {
                throw new ValidationException("Transfer rules require a destination account.");
            }

            if (transferAccountId.Value == accountId)
            {
                throw new ValidationException("Transfer source and destination accounts must be different.");
            }

            transferAccount = await dbContext.Accounts.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == transferAccountId.Value && !x.IsArchived, cancellationToken)
                ?? throw new ValidationException("Selected destination account is invalid or archived.");

            if (categoryId.HasValue)
            {
                throw new ValidationException("Transfer rules cannot be assigned a category.");
            }
        }
        else
        {
            if (!categoryId.HasValue)
            {
                throw new ValidationException("Income and expense rules require a category.");
            }

            category = await dbContext.Categories.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == categoryId.Value && !x.IsArchived, cancellationToken)
                ?? throw new ValidationException("Selected category is invalid or archived.");

            var expected = type == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
            if (category.Type != expected)
            {
                throw new ValidationException("Category type does not match the selected recurring transaction type.");
            }
        }

        return (sourceAccount, transferAccount, category);
    }

    private static RecurringTransactionDto MapRule(RecurringTransactionRule rule, bool canManage)
    {
        var lastProcessedAtUtc = rule.Executions
            .Where(x => x.Status == RecurringExecutionStatus.Completed)
            .OrderByDescending(x => x.ProcessedAtUtc)
            .Select(x => x.ProcessedAtUtc)
            .FirstOrDefault();

        return new RecurringTransactionDto(
            rule.Id,
            rule.Title,
            rule.Type,
            rule.Amount,
            rule.AccountId,
            rule.Account.Name,
            rule.TransferAccountId,
            rule.TransferAccount?.Name,
            rule.CategoryId,
            rule.Category?.Name,
            rule.Frequency,
            rule.StartDateUtc,
            rule.EndDateUtc,
            rule.NextRunDateUtc,
            rule.AutoCreateTransaction,
            rule.Status,
            canManage,
            rule.CreatedUtc,
            rule.UpdatedUtc,
            lastProcessedAtUtc);
    }

    private static TimeSpan ComputeRetryDelay(AutomationOptions options, int attemptCount)
    {
        var baseDelaySeconds = Math.Max(options.InitialRetryDelaySeconds, 15);
        var maxDelaySeconds = Math.Max(options.MaxRetryDelaySeconds, baseDelaySeconds);
        var safeExponent = Math.Min(Math.Max(attemptCount - 1, 0), 10);
        var scaledSeconds = baseDelaySeconds * Math.Pow(2, safeExponent);
        var boundedSeconds = Math.Min(maxDelaySeconds, scaledSeconds);
        return TimeSpan.FromSeconds(Math.Max(baseDelaySeconds, boundedSeconds));
    }

    private static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}


