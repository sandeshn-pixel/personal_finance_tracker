using FinanceTracker.Application.Common;
using FinanceTracker.Application.Rules.DTOs;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Backend.Tests.TestSupport;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Rules;

namespace FinanceTracker.Backend.Tests;

public sealed class RulesEngineTests
{
    [Fact]
    public async Task RuleService_CreateAndListAsync_PersistsUserScopedRules()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var transport = TestData.AddCategory(dbContext, user.Id, "Transport", CategoryType.Expense);
        await dbContext.SaveChangesAsync();

        var service = new RuleService(dbContext);
        await service.CreateAsync(user.Id, new UpsertTransactionRuleRequest(
            "Uber transport",
            10,
            true,
            new RuleConditionDto(RuleConditionField.Merchant, RuleConditionOperator.Contains, "uber", null, null, null, null),
            new RuleActionDto(RuleActionType.SetCategory, transport.Id, null, null, null)), CancellationToken.None);

        var rules = await service.ListAsync(user.Id, CancellationToken.None);
        var rule = Assert.Single(rules);

        Assert.Equal("Uber transport", rule.Name);
        Assert.Equal("Merchant contains 'uber'", rule.ConditionSummary);
        Assert.Equal("Set category to the selected category", rule.ActionSummary);
    }

    [Fact]
    public async Task TransactionRuleEvaluator_AppliesCompatibleActionsInPriorityOrder()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 4000m);
        var transport = TestData.AddCategory(dbContext, user.Id, "Transport", CategoryType.Expense);
        var shopping = TestData.AddCategory(dbContext, user.Id, "Shopping", CategoryType.Expense);

        dbContext.TransactionRules.AddRange(
            CreateRule(user.Id, "Uber category", 10,
                new RuleConditionDto(RuleConditionField.Merchant, RuleConditionOperator.Contains, "uber", null, null, null, null),
                new RuleActionDto(RuleActionType.SetCategory, transport.Id, null, null, null)),
            CreateRule(user.Id, "High amount category", 20,
                new RuleConditionDto(RuleConditionField.Amount, RuleConditionOperator.GreaterThan, null, 500m, null, null, null),
                new RuleActionDto(RuleActionType.SetCategory, shopping.Id, null, null, null)),
            CreateRule(user.Id, "Transport tag", 30,
                new RuleConditionDto(RuleConditionField.Merchant, RuleConditionOperator.Contains, "uber", null, null, null, null),
                new RuleActionDto(RuleActionType.AddTag, null, "monthly-food", null, null)),
            CreateRule(user.Id, "Large spend alert", 40,
                new RuleConditionDto(RuleConditionField.Amount, RuleConditionOperator.GreaterThan, null, 500m, null, null, null),
                new RuleActionDto(RuleActionType.CreateAlert, null, null, "Review spend", "Large transaction recorded.")));

        await dbContext.SaveChangesAsync();

        var evaluator = new TransactionRuleEvaluator(dbContext, new NotificationService(dbContext));
        var result = await evaluator.EvaluateAsync(user.Id, new UpsertTransactionRequest
        {
            AccountId = account.Id,
            Type = TransactionType.Expense,
            Amount = 620m,
            DateUtc = new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc),
            Merchant = "Uber India",
            Tags = []
        }, CancellationToken.None);

        Assert.Equal(transport.Id, result.Request.CategoryId);
        Assert.Contains("monthly-food", result.Request.Tags);
        Assert.Equal(4, result.MatchedRuleIds.Count);
        Assert.Equal("Review spend", Assert.Single(result.Alerts).Title);
    }

    [Fact]
    public async Task TransactionService_CreateAsync_AppliesRulesAndPublishesNotifications()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 5000m);
        var food = TestData.AddCategory(dbContext, user.Id, "Food", CategoryType.Expense);

        dbContext.TransactionRules.AddRange(
            CreateRule(user.Id, "Food by merchant", 10,
                new RuleConditionDto(RuleConditionField.Merchant, RuleConditionOperator.Contains, "swiggy", null, null, null, null),
                new RuleActionDto(RuleActionType.SetCategory, food.Id, null, null, null)),
            CreateRule(user.Id, "Tag food", 20,
                new RuleConditionDto(RuleConditionField.Merchant, RuleConditionOperator.Contains, "swiggy", null, null, null, null),
                new RuleActionDto(RuleActionType.AddTag, null, "takeout", null, null)),
            CreateRule(user.Id, "Alert big food", 30,
                new RuleConditionDto(RuleConditionField.Amount, RuleConditionOperator.GreaterThan, null, 300m, null, null, null),
                new RuleActionDto(RuleActionType.CreateAlert, null, null, "Large food spend", "Check this expense before month end.")));

        await dbContext.SaveChangesAsync();

        var notificationService = new NotificationService(dbContext);
        var evaluator = new TransactionRuleEvaluator(dbContext, notificationService);
        var service = new TransactionService(dbContext, new CategorySeeder(dbContext), evaluator);

        var created = await service.CreateAsync(user.Id, new UpsertTransactionRequest
        {
            AccountId = account.Id,
            Type = TransactionType.Expense,
            Amount = 450m,
            DateUtc = new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc),
            Merchant = "Swiggy",
            Tags = []
        }, CancellationToken.None);

        Assert.Equal(food.Id, created.CategoryId);
        Assert.Contains("takeout", created.Tags);
        Assert.Equal(4550m, dbContext.Accounts.Single(x => x.Id == account.Id).CurrentBalance);
        Assert.Equal(NotificationType.RuleTriggeredAlert, Assert.Single(dbContext.UserNotifications).Type);
    }

    [Fact]
    public async Task RuleService_CreateAsync_RejectsArchivedReferencedCategory()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var archived = TestData.AddCategory(dbContext, user.Id, "Archived", CategoryType.Expense);
        archived.IsArchived = true;
        await dbContext.SaveChangesAsync();

        var service = new RuleService(dbContext);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(user.Id, new UpsertTransactionRuleRequest(
            "Archived category",
            10,
            true,
            new RuleConditionDto(RuleConditionField.Merchant, RuleConditionOperator.Contains, "shop", null, null, null, null),
            new RuleActionDto(RuleActionType.SetCategory, archived.Id, null, null, null)), CancellationToken.None));

        Assert.Contains("invalid or archived", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TransactionRule CreateRule(Guid userId, string name, int priority, RuleConditionDto condition, RuleActionDto action)
        => new()
        {
            UserId = userId,
            Name = name,
            Priority = priority,
            ConditionJson = System.Text.Json.JsonSerializer.Serialize(condition, JsonOptions),
            ActionJson = System.Text.Json.JsonSerializer.Serialize(action, JsonOptions),
            IsActive = true
        };

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };
}
