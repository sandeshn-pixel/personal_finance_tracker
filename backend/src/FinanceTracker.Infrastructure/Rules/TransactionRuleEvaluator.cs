using FinanceTracker.Application.Notifications.DTOs;
using FinanceTracker.Application.Notifications.Interfaces;
using FinanceTracker.Application.Rules.DTOs;
using FinanceTracker.Application.Rules.Interfaces;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Rules;

public sealed class TransactionRuleEvaluator(
    ApplicationDbContext dbContext,
    INotificationService notificationService) : ITransactionRuleEvaluator
{
    public async Task<RuleEvaluationResult> EvaluateAsync(Guid userId, UpsertTransactionRequest request, CancellationToken cancellationToken)
    {
        var activeRules = await dbContext.TransactionRules
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);

        if (activeRules.Count == 0)
        {
            return new RuleEvaluationResult(request, [], []);
        }

        var referencedCategoryIds = activeRules
            .SelectMany(x =>
            {
                var condition = RuleJsonSerializer.Deserialize<RuleConditionDto>(x.ConditionJson);
                var action = RuleJsonSerializer.Deserialize<RuleActionDto>(x.ActionJson);
                return new[] { condition.CategoryId, action.CategoryId };
            })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var referencedAccountIds = activeRules
            .Select(x => RuleJsonSerializer.Deserialize<RuleConditionDto>(x.ConditionJson).AccountId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsArchived && referencedCategoryIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsArchived && referencedAccountIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var original = EvaluatedTransactionCandidate.FromRequest(request);
        var mutated = EvaluatedTransactionCandidate.FromRequest(request);
        var matchedRuleIds = new List<Guid>();
        var alerts = new List<RuleEvaluationAlert>();
        var categoryAlreadySet = request.CategoryId.HasValue;

        foreach (var rule in activeRules)
        {
            var condition = RuleJsonSerializer.Deserialize<RuleConditionDto>(rule.ConditionJson);
            if (!Matches(condition, original, categories, accounts))
            {
                continue;
            }

            matchedRuleIds.Add(rule.Id);
            var action = RuleJsonSerializer.Deserialize<RuleActionDto>(rule.ActionJson);
            ApplyAction(rule, action, mutated, categories, ref categoryAlreadySet, alerts);
        }

        return new RuleEvaluationResult(mutated.ToRequest(), matchedRuleIds, alerts);
    }

    public async Task PublishAlertsAsync(Guid userId, Guid transactionId, IEnumerable<RuleEvaluationAlert> alerts, CancellationToken cancellationToken)
    {
        foreach (var alert in alerts)
        {
            await notificationService.PublishAsync(new PublishNotificationRequest(
                userId,
                NotificationType.RuleTriggeredAlert,
                NotificationLevel.Warning,
                alert.Title,
                alert.Message,
                "/transactions",
                $"rule-alert:{alert.RuleId}:{transactionId}"), cancellationToken);
        }
    }

    private static bool Matches(
        RuleConditionDto condition,
        EvaluatedTransactionCandidate candidate,
        IReadOnlyDictionary<Guid, Category> categories,
        IReadOnlyDictionary<Guid, Account> accounts)
    {
        return condition.Field switch
        {
            RuleConditionField.Merchant when condition.Operator == RuleConditionOperator.Equals =>
                string.Equals(candidate.Merchant, condition.TextValue?.Trim(), StringComparison.OrdinalIgnoreCase),
            RuleConditionField.Merchant when condition.Operator == RuleConditionOperator.Contains =>
                !string.IsNullOrWhiteSpace(candidate.Merchant)
                && !string.IsNullOrWhiteSpace(condition.TextValue)
                && candidate.Merchant.Contains(condition.TextValue.Trim(), StringComparison.OrdinalIgnoreCase),
            RuleConditionField.Amount when condition.Operator == RuleConditionOperator.GreaterThan =>
                condition.AmountValue.HasValue && candidate.Amount > condition.AmountValue.Value,
            RuleConditionField.Amount when condition.Operator == RuleConditionOperator.LessThan =>
                condition.AmountValue.HasValue && candidate.Amount < condition.AmountValue.Value,
            RuleConditionField.Category when condition.Operator == RuleConditionOperator.Equals =>
                condition.CategoryId.HasValue
                && categories.ContainsKey(condition.CategoryId.Value)
                && candidate.CategoryId == condition.CategoryId.Value,
            RuleConditionField.TransactionType when condition.Operator == RuleConditionOperator.Equals =>
                condition.TransactionType.HasValue && candidate.Type == condition.TransactionType.Value,
            RuleConditionField.Account when condition.Operator == RuleConditionOperator.Equals =>
                condition.AccountId.HasValue
                && accounts.ContainsKey(condition.AccountId.Value)
                && candidate.AccountId == condition.AccountId.Value,
            _ => false
        };
    }

    private static void ApplyAction(
        TransactionRule rule,
        RuleActionDto action,
        EvaluatedTransactionCandidate candidate,
        IReadOnlyDictionary<Guid, Category> categories,
        ref bool categoryAlreadySet,
        List<RuleEvaluationAlert> alerts)
    {
        switch (action.Type)
        {
            case RuleActionType.SetCategory:
                if (categoryAlreadySet || candidate.Type == TransactionType.Transfer || !action.CategoryId.HasValue)
                {
                    return;
                }

                if (!categories.TryGetValue(action.CategoryId.Value, out var category))
                {
                    return;
                }

                var expectedCategoryType = candidate.Type == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
                if (category.Type != expectedCategoryType)
                {
                    return;
                }

                candidate.CategoryId = category.Id;
                categoryAlreadySet = true;
                break;
            case RuleActionType.AddTag:
                if (!string.IsNullOrWhiteSpace(action.Tag))
                {
                    candidate.Tags.Add(action.Tag.Trim());
                }
                break;
            case RuleActionType.CreateAlert:
                if (!string.IsNullOrWhiteSpace(action.AlertTitle) && !string.IsNullOrWhiteSpace(action.AlertMessage))
                {
                    alerts.Add(new RuleEvaluationAlert(rule.Id, rule.Name, action.AlertTitle.Trim(), action.AlertMessage.Trim()));
                }
                break;
        }
    }

    private sealed class EvaluatedTransactionCandidate
    {
        public Guid AccountId { get; init; }
        public Guid? TransferAccountId { get; init; }
        public TransactionType Type { get; init; }
        public decimal Amount { get; init; }
        public DateTime DateUtc { get; init; }
        public Guid? CategoryId { get; set; }
        public string? Note { get; init; }
        public string? Merchant { get; init; }
        public string? PaymentMethod { get; init; }
        public Guid? RecurringTransactionId { get; init; }
        public List<string> Tags { get; } = [];

        public static EvaluatedTransactionCandidate FromRequest(UpsertTransactionRequest request)
        {
            var candidate = new EvaluatedTransactionCandidate
            {
                AccountId = request.AccountId,
                TransferAccountId = request.TransferAccountId,
                Type = request.Type,
                Amount = request.Amount,
                DateUtc = request.DateUtc,
                CategoryId = request.CategoryId,
                Note = request.Note?.Trim(),
                Merchant = request.Merchant?.Trim(),
                PaymentMethod = request.PaymentMethod?.Trim(),
                RecurringTransactionId = request.RecurringTransactionId
            };

            candidate.Tags.AddRange(TransactionMapping.NormalizeTags(request.Tags));
            return candidate;
        }

        public UpsertTransactionRequest ToRequest() => new()
        {
            AccountId = AccountId,
            TransferAccountId = TransferAccountId,
            Type = Type,
            Amount = Amount,
            DateUtc = DateUtc,
            CategoryId = CategoryId,
            Note = Note,
            Merchant = Merchant,
            PaymentMethod = PaymentMethod,
            RecurringTransactionId = RecurringTransactionId,
            Tags = TransactionMapping.NormalizeTags(Tags)
        };
    }
}
