using FinanceTracker.Application.Accounts.DTOs;
using FinanceTracker.Application.Budgets.DTOs;
using FinanceTracker.Application.Goals.DTOs;
using FinanceTracker.Application.RecurringTransactions.DTOs;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var cancellationToken = CancellationToken.None;
var rootPath = ResolveRootPath();
var connectionString = LoadConnectionString(rootPath);
var today = DateTime.UtcNow.Date;

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var dbContext = new ApplicationDbContext(options);
var categorySeeder = new CategorySeeder(dbContext);
var notificationService = new NotificationService(dbContext);
var accountService = new AccountService(dbContext);
var transactionService = new TransactionService(dbContext, categorySeeder);
var budgetService = new BudgetService(dbContext);
var goalService = new GoalService(dbContext, notificationService);
var recurringService = new RecurringTransactionService(
    dbContext,
    transactionService,
    notificationService,
    Microsoft.Extensions.Options.Options.Create(new FinanceTracker.Infrastructure.Automation.AutomationOptions()));

var users = await dbContext.Users
    .AsNoTracking()
    .OrderBy(x => x.CreatedUtc)
    .ToListAsync(cancellationToken);

if (users.Count == 0)
{
    Console.WriteLine("No users found. Register a user first, then run the seeder again.");
    return;
}

foreach (var user in users)
{
    Console.WriteLine($"Seeding sample data for {user.Email}...");
    await categorySeeder.EnsureDefaultsAsync(user.Id, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);

    var accounts = await EnsureAccountsAsync(user.Id);
    await EnsureTransactionsAsync(user.Id, accounts);
    await EnsureBudgetsAsync(user.Id);
    await EnsureGoalsAsync(user.Id, accounts);
    await EnsureRecurringRulesAsync(user.Id, accounts);
}

Console.WriteLine("Sample data seeding completed.");

async Task<Dictionary<string, Account>> EnsureAccountsAsync(Guid userId)
{
    var accounts = await dbContext.Accounts
        .Where(x => x.UserId == userId && !x.IsArchived)
        .ToListAsync(cancellationToken);

    async Task AddIfMissingAsync(string name, AccountType type, decimal openingBalance, string institution, string? last4Digits)
    {
        if (accounts.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        await accountService.CreateAsync(userId, new CreateAccountRequest
        {
            Name = name,
            Type = type,
            CurrencyCode = "INR",
            OpeningBalance = openingBalance,
            InstitutionName = institution,
            Last4Digits = last4Digits
        }, cancellationToken);
    }

    await AddIfMissingAsync("Salary Account", AccountType.BankAccount, 80000m, "Axis Bank", "4217");
    await AddIfMissingAsync("Emergency Savings", AccountType.SavingsAccount, 25000m, "HDFC Bank", "9901");
    await AddIfMissingAsync("Cash Wallet", AccountType.CashWallet, 3000m, "Cash", null);

    return await dbContext.Accounts
        .Where(x => x.UserId == userId && !x.IsArchived)
        .ToDictionaryAsync(x => x.Name, cancellationToken);
}

async Task EnsureTransactionsAsync(Guid userId, Dictionary<string, Account> accounts)
{
    var categories = await dbContext.Categories
        .Where(x => x.UserId == userId && !x.IsArchived)
        .ToDictionaryAsync(x => (x.Name, x.Type), cancellationToken);

    var salaryAccount = accounts["Salary Account"];
    var savingsAccount = accounts["Emergency Savings"];
    var walletAccount = accounts["Cash Wallet"];

    var seedMonths = Enumerable.Range(0, 4)
        .Select(offset => new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(offset - 3))
        .ToList();

    foreach (var monthStart in seedMonths)
    {
        await SeedTransactionIfMissingAsync(
            userId,
            monthStart,
            "Monthly salary",
            new UpsertTransactionRequest
            {
                AccountId = salaryAccount.Id,
                Type = TransactionType.Income,
                Amount = monthStart.Month == today.Month ? 98000m : 95000m + ((monthStart.Month % 3) * 1500m),
                DateUtc = monthStart,
                CategoryId = categories[("Salary", CategoryType.Income)].Id,
                Note = "Seed sample: Monthly salary",
                Merchant = "Tech Corp",
                PaymentMethod = "Bank Transfer",
                Tags = ["salary", "income"]
            });

        await SeedTransactionIfMissingAsync(
            userId,
            monthStart.AddDays(2),
            "Savings transfer",
            new UpsertTransactionRequest
            {
                AccountId = salaryAccount.Id,
                TransferAccountId = savingsAccount.Id,
                Type = TransactionType.Transfer,
                Amount = monthStart.Month == today.Month ? 12000m : 10000m,
                DateUtc = monthStart.AddDays(2),
                Note = "Seed sample: Savings transfer",
                PaymentMethod = "Internal Transfer",
                Tags = ["savings"]
            });

        await SeedTransactionIfMissingAsync(
            userId,
            monthStart.AddDays(3),
            "Monthly rent",
            new UpsertTransactionRequest
            {
                AccountId = salaryAccount.Id,
                Type = TransactionType.Expense,
                Amount = 22000m,
                DateUtc = monthStart.AddDays(3),
                CategoryId = categories[("Rent", CategoryType.Expense)].Id,
                Note = "Seed sample: Monthly rent",
                Merchant = "Green Residency",
                PaymentMethod = "UPI",
                Tags = ["home"]
            });

        await SeedTransactionIfMissingAsync(
            userId,
            monthStart.AddDays(5),
            "Utilities bill",
            new UpsertTransactionRequest
            {
                AccountId = salaryAccount.Id,
                Type = TransactionType.Expense,
                Amount = 2600m + ((monthStart.Month % 2) * 250m),
                DateUtc = monthStart.AddDays(5),
                CategoryId = categories[("Utilities", CategoryType.Expense)].Id,
                Note = "Seed sample: Utilities bill",
                Merchant = "BESCOM",
                PaymentMethod = "Net Banking",
                Tags = ["home", "utilities"]
            });

        await SeedTransactionIfMissingAsync(
            userId,
            monthStart.AddDays(6),
            "Fuel and cabs",
            new UpsertTransactionRequest
            {
                AccountId = walletAccount.Id,
                Type = TransactionType.Expense,
                Amount = 1350m + ((monthStart.Month % 3) * 180m),
                DateUtc = monthStart.AddDays(6),
                CategoryId = categories[("Transport", CategoryType.Expense)].Id,
                Note = "Seed sample: Fuel and cabs",
                Merchant = "Uber",
                PaymentMethod = "Cash",
                Tags = ["commute"]
            });

        await SeedTransactionIfMissingAsync(
            userId,
            monthStart.AddDays(7),
            "Groceries and dining",
            new UpsertTransactionRequest
            {
                AccountId = salaryAccount.Id,
                Type = TransactionType.Expense,
                Amount = 3200m + ((monthStart.Month % 3) * 350m),
                DateUtc = monthStart.AddDays(7),
                CategoryId = categories[("Food", CategoryType.Expense)].Id,
                Note = "Seed sample: Groceries and dining",
                Merchant = "BigBasket",
                PaymentMethod = "Credit Card",
                Tags = ["food", "home"]
            });

        await SeedTransactionIfMissingAsync(
            userId,
            monthStart.AddDays(10),
            "Household shopping",
            new UpsertTransactionRequest
            {
                AccountId = salaryAccount.Id,
                Type = TransactionType.Expense,
                Amount = 3900m + ((monthStart.Month % 4) * 220m),
                DateUtc = monthStart.AddDays(10),
                CategoryId = categories[("Shopping", CategoryType.Expense)].Id,
                Note = "Seed sample: Household shopping",
                Merchant = "Amazon",
                PaymentMethod = "Credit Card",
                Tags = ["shopping"]
            });

        await SeedTransactionIfMissingAsync(
            userId,
            monthStart.AddDays(12),
            "Streaming subscription",
            new UpsertTransactionRequest
            {
                AccountId = salaryAccount.Id,
                Type = TransactionType.Expense,
                Amount = 999m,
                DateUtc = monthStart.AddDays(12),
                CategoryId = categories[("Subscriptions", CategoryType.Expense)].Id,
                Note = "Seed sample: Streaming subscription",
                Merchant = "Netflix",
                PaymentMethod = "Auto Debit",
                Tags = ["subscription"]
            });

        await SeedTransactionIfMissingAsync(
            userId,
            monthStart.AddDays(13),
            "Weekend outing",
            new UpsertTransactionRequest
            {
                AccountId = salaryAccount.Id,
                Type = TransactionType.Expense,
                Amount = 2800m + ((monthStart.Month % 4) * 260m),
                DateUtc = monthStart.AddDays(13),
                CategoryId = categories[("Entertainment", CategoryType.Expense)].Id,
                Note = "Seed sample: Weekend outing",
                Merchant = "PVR Cinemas",
                PaymentMethod = "UPI",
                Tags = ["weekend", "fun"]
            });
    }

    async Task SeedTransactionIfMissingAsync(Guid seedUserId, DateTime occurredAtUtc, string label, UpsertTransactionRequest request)
    {
        var expectedNote = $"Seed sample: {label}";
        var exists = await dbContext.Transactions.AnyAsync(x =>
            x.UserId == seedUserId &&
            !x.IsDeleted &&
            x.Note == expectedNote &&
            x.DateUtc == occurredAtUtc,
            cancellationToken);

        if (exists)
        {
            return;
        }

        await transactionService.CreateAsync(seedUserId, request, cancellationToken);
    }
}

async Task EnsureBudgetsAsync(Guid userId)
{
    var categories = await dbContext.Categories
        .Where(x => x.UserId == userId && !x.IsArchived && x.Type == CategoryType.Expense)
        .ToDictionaryAsync(x => x.Name, cancellationToken);

    var seedMonths = Enumerable.Range(0, 4)
        .Select(offset => new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(offset - 3))
        .ToList();

    var budgetPlan = new Dictionary<string, decimal>
    {
        ["Rent"] = 25000m,
        ["Food"] = 9000m,
        ["Utilities"] = 4500m,
        ["Transport"] = 5000m,
        ["Shopping"] = 7000m,
        ["Subscriptions"] = 2000m
    };

    foreach (var monthStart in seedMonths)
    {
        var existingCategoryIds = await dbContext.Budgets
            .Where(x => x.UserId == userId && x.Year == monthStart.Year && x.Month == monthStart.Month)
            .Select(x => x.CategoryId)
            .ToListAsync(cancellationToken);

        foreach (var item in budgetPlan)
        {
            var category = categories[item.Key];
            if (existingCategoryIds.Contains(category.Id))
            {
                continue;
            }

            await budgetService.CreateAsync(userId, new CreateBudgetRequest(
                category.Id,
                monthStart.Year,
                monthStart.Month,
                item.Value,
                80), cancellationToken);
        }
    }
}

async Task EnsureGoalsAsync(Guid userId, Dictionary<string, Account> accounts)
{
    var existingGoals = await dbContext.Goals
        .Where(x => x.UserId == userId)
        .ToListAsync(cancellationToken);

    var savingsAccount = accounts["Emergency Savings"];

    async Task SeedGoalAsync(string name, decimal targetAmount, DateTime? targetDateUtc, string icon, string color, decimal contributionAmount, string contributionNote)
    {
        var goal = existingGoals.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (goal is null)
        {
            var created = await goalService.CreateAsync(userId, new CreateGoalRequest
            {
                Name = name,
                TargetAmount = targetAmount,
                TargetDateUtc = targetDateUtc,
                LinkedAccountId = savingsAccount.Id,
                Icon = icon,
                Color = color
            }, cancellationToken);

            goal = await dbContext.Goals.SingleAsync(x => x.Id == created.Id, cancellationToken);
            existingGoals.Add(goal);
        }

        var hasEntries = await dbContext.GoalEntries.AnyAsync(x => x.UserId == userId && x.GoalId == goal.Id, cancellationToken);
        if (!hasEntries)
        {
            await goalService.RecordContributionAsync(userId, goal.Id, new RecordGoalEntryRequest
            {
                Amount = contributionAmount,
                OccurredAtUtc = DateTime.UtcNow.AddDays(-5),
                Note = contributionNote
            }, cancellationToken);
        }
    }

    await SeedGoalAsync("Emergency Fund", 150000m, DateTime.UtcNow.AddMonths(8), "Shield", "teal", 12000m, "Seed sample: emergency savings");
    await SeedGoalAsync("Vacation 2026", 60000m, DateTime.UtcNow.AddMonths(5), "Plane", "amber", 7000m, "Seed sample: travel savings");
}

async Task EnsureRecurringRulesAsync(Guid userId, Dictionary<string, Account> accounts)
{
    var existingTitles = await dbContext.RecurringTransactionRules
        .Where(x => x.UserId == userId && x.Status != RecurringRuleStatus.Deleted)
        .Select(x => x.Title)
        .ToListAsync(cancellationToken);

    var categories = await dbContext.Categories
        .Where(x => x.UserId == userId && !x.IsArchived)
        .ToDictionaryAsync(x => (x.Name, x.Type), cancellationToken);

    var salaryAccount = accounts["Salary Account"];
    var savingsAccount = accounts["Emergency Savings"];
    var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var monthEnd = monthStart.AddMonths(1).AddDays(-1);
    var dueDates = new[]
    {
        monthStart.AddDays(Math.Min(Math.Max(today.Day + 1, 2), monthEnd.Day) - 1),
        monthStart.AddDays(Math.Min(Math.Max(today.Day + 3, 4), monthEnd.Day) - 1),
        monthStart.AddDays(Math.Min(Math.Max(today.Day + 5, 5), monthEnd.Day) - 1)
    };

    async Task AddRuleAsync(CreateRecurringTransactionRequest request)
    {
        if (existingTitles.Any(x => string.Equals(x, request.Title, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        await recurringService.CreateAsync(userId, request, cancellationToken);
    }

    await AddRuleAsync(new CreateRecurringTransactionRequest
    {
        Title = "Monthly Rent",
        Type = TransactionType.Expense,
        Amount = 22000m,
        CategoryId = categories[("Rent", CategoryType.Expense)].Id,
        AccountId = salaryAccount.Id,
        Frequency = RecurringFrequency.Monthly,
        StartDateUtc = dueDates[0],
        AutoCreateTransaction = true
    });

    await AddRuleAsync(new CreateRecurringTransactionRequest
    {
        Title = "Streaming Plan",
        Type = TransactionType.Expense,
        Amount = 999m,
        CategoryId = categories[("Subscriptions", CategoryType.Expense)].Id,
        AccountId = salaryAccount.Id,
        Frequency = RecurringFrequency.Monthly,
        StartDateUtc = dueDates[1],
        AutoCreateTransaction = true
    });

    await AddRuleAsync(new CreateRecurringTransactionRequest
    {
        Title = "Monthly Freelance Payment",
        Type = TransactionType.Income,
        Amount = 18000m,
        CategoryId = categories[("Salary", CategoryType.Income)].Id,
        AccountId = salaryAccount.Id,
        Frequency = RecurringFrequency.Monthly,
        StartDateUtc = dueDates[2],
        AutoCreateTransaction = true
    });

    await AddRuleAsync(new CreateRecurringTransactionRequest
    {
        Title = "Monthly Savings Sweep",
        Type = TransactionType.Transfer,
        Amount = 5000m,
        AccountId = salaryAccount.Id,
        TransferAccountId = savingsAccount.Id,
        Frequency = RecurringFrequency.Monthly,
        StartDateUtc = dueDates[2],
        AutoCreateTransaction = true
    });
}

static string ResolveRootPath()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "backend", ".env")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Could not locate the repository root.");
}

static string LoadConnectionString(string rootPath)
{
    var envPath = Path.Combine(rootPath, "backend", ".env");
    if (!File.Exists(envPath))
    {
        throw new InvalidOperationException("backend/.env was not found.");
    }

    foreach (var rawLine in File.ReadAllLines(envPath))
    {
        var line = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"');
        if (string.Equals(key, "ConnectionStrings__DefaultConnection", StringComparison.Ordinal))
        {
            return value;
        }
    }

    throw new InvalidOperationException("ConnectionStrings__DefaultConnection was not found in backend/.env.");
}
