using System.Text.Json;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Rules.DTOs;
using FinanceTracker.Application.Rules.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Rules;

public sealed class RuleService(ApplicationDbContext dbContext) : IRuleService
{
    public async Task<IReadOnlyCollection<TransactionRuleDto>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rules = await dbContext.TransactionRules
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);

        return rules.Select(MapToDto).ToList();
    }

    public async Task<TransactionRuleDto> CreateAsync(Guid userId, UpsertTransactionRuleRequest request, CancellationToken cancellationToken)
    {
        await ValidateReferencesAsync(userId, request, cancellationToken);

        var entity = new TransactionRule
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Priority = request.Priority,
            IsActive = request.IsActive,
            ConditionJson = RuleJsonSerializer.Serialize(request.Condition),
            ActionJson = RuleJsonSerializer.Serialize(request.Action)
        };

        dbContext.TransactionRules.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    public async Task<TransactionRuleDto> UpdateAsync(Guid userId, Guid ruleId, UpsertTransactionRuleRequest request, CancellationToken cancellationToken)
    {
        await ValidateReferencesAsync(userId, request, cancellationToken);

        var entity = await dbContext.TransactionRules
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == ruleId, cancellationToken)
            ?? throw new NotFoundException("Rule was not found.");

        entity.Name = request.Name.Trim();
        entity.Priority = request.Priority;
        entity.IsActive = request.IsActive;
        entity.ConditionJson = RuleJsonSerializer.Serialize(request.Condition);
        entity.ActionJson = RuleJsonSerializer.Serialize(request.Action);

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.TransactionRules
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == ruleId, cancellationToken)
            ?? throw new NotFoundException("Rule was not found.");

        dbContext.TransactionRules.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateReferencesAsync(Guid userId, UpsertTransactionRuleRequest request, CancellationToken cancellationToken)
    {
        if (request.Condition.Field == RuleConditionField.Category && request.Condition.CategoryId.HasValue)
        {
            var categoryExists = await dbContext.Categories.AnyAsync(x => x.UserId == userId && x.Id == request.Condition.CategoryId.Value && !x.IsArchived, cancellationToken);
            if (!categoryExists)
            {
                throw new ValidationException("Selected rule condition category is invalid or archived.");
            }
        }

        if (request.Condition.Field == RuleConditionField.Account && request.Condition.AccountId.HasValue)
        {
            var accountExists = await dbContext.Accounts.AnyAsync(x => x.UserId == userId && x.Id == request.Condition.AccountId.Value && !x.IsArchived, cancellationToken);
            if (!accountExists)
            {
                throw new ValidationException("Selected rule condition account is invalid or archived.");
            }
        }

        if (request.Action.Type == RuleActionType.SetCategory && request.Action.CategoryId.HasValue)
        {
            var categoryExists = await dbContext.Categories.AnyAsync(x => x.UserId == userId && x.Id == request.Action.CategoryId.Value && !x.IsArchived, cancellationToken);
            if (!categoryExists)
            {
                throw new ValidationException("Selected rule action category is invalid or archived.");
            }
        }
    }

    private static TransactionRuleDto MapToDto(TransactionRule entity)
    {
        var condition = RuleJsonSerializer.Deserialize<RuleConditionDto>(entity.ConditionJson);
        var action = RuleJsonSerializer.Deserialize<RuleActionDto>(entity.ActionJson);

        return new TransactionRuleDto(
            entity.Id,
            entity.Name,
            entity.Priority,
            entity.IsActive,
            condition,
            action,
            BuildConditionSummary(condition),
            BuildActionSummary(action),
            entity.CreatedUtc,
            entity.UpdatedUtc);
    }

    internal static string BuildConditionSummary(RuleConditionDto condition)
        => condition.Field switch
        {
            RuleConditionField.Merchant when condition.Operator == RuleConditionOperator.Equals => $"Merchant equals '{condition.TextValue?.Trim()}'",
            RuleConditionField.Merchant when condition.Operator == RuleConditionOperator.Contains => $"Merchant contains '{condition.TextValue?.Trim()}'",
            RuleConditionField.Amount when condition.Operator == RuleConditionOperator.GreaterThan => $"Amount is greater than {condition.AmountValue:0.##}",
            RuleConditionField.Amount when condition.Operator == RuleConditionOperator.LessThan => $"Amount is less than {condition.AmountValue:0.##}",
            RuleConditionField.Category => "Category matches the selected category",
            RuleConditionField.TransactionType => $"Transaction type equals {condition.TransactionType}",
            RuleConditionField.Account => "Account matches the selected account",
            _ => "Condition"
        };

    internal static string BuildActionSummary(RuleActionDto action)
        => action.Type switch
        {
            RuleActionType.SetCategory => "Set category to the selected category",
            RuleActionType.AddTag => $"Add tag '{action.Tag?.Trim()}'",
            RuleActionType.CreateAlert => $"Create alert '{action.AlertTitle?.Trim()}'",
            _ => "Action"
        };
}

internal static class RuleJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException("Stored rule payload could not be deserialized.");
}
