using FinanceTracker.Application.Common;
using FinanceTracker.Application.RecurringTransactions.DTOs;
using FinanceTracker.Application.RecurringTransactions.Interfaces;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Application.Transactions.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class RecurringTransactionService(ApplicationDbContext dbContext, ITransactionService transactionService) : IRecurringTransactionService
{
    public async Task<IReadOnlyCollection<RecurringTransactionDto>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rules = await dbContext.RecurringTransactionRules
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Status != RecurringRuleStatus.Deleted)
            .Include(x => x.Account)
            .Include(x => x.TransferAccount)
            .Include(x => x.Category)
            .Include(x => x.Executions)
            .OrderBy(x => x.Status)
            .ThenBy(x => x.NextRunDateUtc)
            .ThenBy(x => x.Title)
            .ToListAsync(cancellationToken);

        return rules.Select(MapRule).ToList();
    }

    public async Task<RecurringTransactionDto?> GetAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken)
    {
        var rule = await dbContext.RecurringTransactionRules
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Id == ruleId && x.Status != RecurringRuleStatus.Deleted)
            .Include(x => x.Account)
            .Include(x => x.TransferAccount)
            .Include(x => x.Category)
            .Include(x => x.Executions)
            .SingleOrDefaultAsync(cancellationToken);

        return rule is null ? null : MapRule(rule);
    }

    public async Task<RecurringTransactionDto> CreateAsync(Guid userId, CreateRecurringTransactionRequest request, CancellationToken cancellationToken)
    {
        var resolved = await ResolveReferencesAsync(userId, request.Type, request.AccountId, request.TransferAccountId, request.CategoryId, cancellationToken);
        var startDate = NormalizeDate(request.StartDateUtc);
        DateTime? endDate = request.EndDateUtc.HasValue ? NormalizeDate(request.EndDateUtc.Value) : null;

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
            NextRunDateUtc = CalculateInitialNextRun(startDate, endDate),
            AutoCreateTransaction = request.AutoCreateTransaction,
            Status = endDate.HasValue && startDate > endDate.Value ? RecurringRuleStatus.Completed : RecurringRuleStatus.Active
        };

        dbContext.RecurringTransactionRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        rule.Account = resolved.SourceAccount;
        rule.TransferAccount = resolved.TransferAccount;
        rule.Category = resolved.Category;
        return MapRule(rule);
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
        var startDate = NormalizeDate(request.StartDateUtc);
        DateTime? endDate = request.EndDateUtc.HasValue ? NormalizeDate(request.EndDateUtc.Value) : null;

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
            rule.NextRunDateUtc = RecalculateNextRunDate(rule, rule.Executions);
            rule.Status = rule.NextRunDateUtc.HasValue
                ? (rule.Status == RecurringRuleStatus.Paused ? RecurringRuleStatus.Paused : RecurringRuleStatus.Active)
                : RecurringRuleStatus.Completed;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapRule(rule);
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
        return MapRule(rule);
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

        rule.NextRunDateUtc = RecalculateNextRunDate(rule, rule.Executions);
        rule.Status = rule.NextRunDateUtc.HasValue ? RecurringRuleStatus.Active : RecurringRuleStatus.Completed;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapRule(rule);
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
        var normalizedAsOf = NormalizeDate(asOfUtc);
        var rules = await dbContext.RecurringTransactionRules
            .Where(x => x.UserId == userId && x.Status == RecurringRuleStatus.Active && x.AutoCreateTransaction && x.NextRunDateUtc != null && x.NextRunDateUtc <= normalizedAsOf)
            .OrderBy(x => x.NextRunDateUtc)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var rulesVisited = 0;
        var transactionsCreated = 0;
        var occurrencesProcessed = 0;
        var occurrencesSkipped = 0;

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
                keepProcessing = processed.ShouldContinue;
            }
        }

        return new RecurringExecutionSummaryDto(rulesVisited, transactionsCreated, occurrencesProcessed, occurrencesSkipped, DateTime.UtcNow);
    }

    private async Task<(bool ShouldContinue, bool TransactionCreated, bool OccurrenceProcessed, bool OccurrenceSkipped)> ProcessSingleOccurrenceAsync(Guid userId, Guid ruleId, DateTime asOfUtc, CancellationToken cancellationToken)
    {
        var rule = await dbContext.RecurringTransactionRules
            .Include(x => x.Executions)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == ruleId, cancellationToken);

        if (rule is null || rule.Status != RecurringRuleStatus.Active || rule.NextRunDateUtc is null || rule.NextRunDateUtc > asOfUtc || !rule.AutoCreateTransaction)
        {
            return (false, false, false, false);
        }

        var scheduledDate = NormalizeDate(rule.NextRunDateUtc.Value);
        var execution = rule.Executions.SingleOrDefault(x => x.ScheduledForDateUtc == scheduledDate);
        if (execution is not null)
        {
            var recovered = await TryRecoverExecutionAsync(userId, rule, execution, cancellationToken);
            if (recovered)
            {
                await AdvanceRuleAsync(rule, scheduledDate);
                return (rule.NextRunDateUtc is not null && rule.NextRunDateUtc <= asOfUtc, false, true, false);
            }

            if (execution.Status == RecurringExecutionStatus.Processing && execution.CreatedUtc > DateTime.UtcNow.AddMinutes(-5))
            {
                return (false, false, false, true);
            }
        }

        if (execution is null)
        {
            execution = new RecurringTransactionExecution
            {
                RecurringTransactionRuleId = rule.Id,
                ScheduledForDateUtc = scheduledDate,
                Status = RecurringExecutionStatus.Processing
            };
            dbContext.RecurringTransactionExecutions.Add(execution);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return (false, false, false, true);
            }
        }
        else
        {
            execution.Status = RecurringExecutionStatus.Processing;
            execution.FailureReason = null;
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
            await AdvanceRuleAsync(rule, scheduledDate);
            await dbContext.SaveChangesAsync(cancellationToken);

            return (rule.NextRunDateUtc is not null && rule.NextRunDateUtc <= asOfUtc, true, true, false);
        }
        catch (ApplicationExceptionBase ex)
        {
            execution.Status = RecurringExecutionStatus.Failed;
            execution.FailureReason = ex.Message.Length > 280 ? ex.Message[..280] : ex.Message;
            execution.ProcessedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return (false, false, false, true);
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
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static Task AdvanceRuleAsync(RecurringTransactionRule rule, DateTime processedDate)
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

        return Task.CompletedTask;
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

    private static RecurringTransactionDto MapRule(RecurringTransactionRule rule)
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
            rule.CreatedUtc,
            rule.UpdatedUtc,
            lastProcessedAtUtc);
    }

    private static DateTime? RecalculateNextRunDate(RecurringTransactionRule rule, IEnumerable<RecurringTransactionExecution> executions)
    {
        var nextRun = rule.StartDateUtc;
        var lastCompleted = executions
            .Where(x => x.Status == RecurringExecutionStatus.Completed)
            .OrderByDescending(x => x.ScheduledForDateUtc)
            .Select(x => (DateTime?)x.ScheduledForDateUtc)
            .FirstOrDefault();

        if (lastCompleted.HasValue)
        {
            nextRun = CalculateNextOccurrence(rule.Frequency, lastCompleted.Value);
        }

        if (rule.EndDateUtc.HasValue && nextRun > NormalizeDate(rule.EndDateUtc.Value))
        {
            return null;
        }

        return nextRun;
    }

    private static DateTime? CalculateInitialNextRun(DateTime startDateUtc, DateTime? endDateUtc)
    {
        if (endDateUtc.HasValue && startDateUtc > endDateUtc.Value)
        {
            return null;
        }

        return startDateUtc;
    }

    private static DateTime CalculateNextOccurrence(RecurringFrequency frequency, DateTime currentDateUtc)
        => frequency switch
        {
            RecurringFrequency.Daily => currentDateUtc.AddDays(1),
            RecurringFrequency.Weekly => currentDateUtc.AddDays(7),
            RecurringFrequency.Monthly => currentDateUtc.AddMonths(1),
            RecurringFrequency.Yearly => currentDateUtc.AddYears(1),
            _ => throw new ValidationException("Unsupported recurring frequency.")
        };

    private static DateTime NormalizeDate(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    private static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}