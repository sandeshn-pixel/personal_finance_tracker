using FinanceTracker.Application.Common;
using FinanceTracker.Application.Categories.Interfaces;
using FinanceTracker.Application.Settings.DTOs;
using FinanceTracker.Application.Settings.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Settings;

public sealed class SettingsService(
    ApplicationDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    ICategorySeeder categorySeeder) : ISettingsService
{
    private static readonly HashSet<string> AllowedThemes = new(StringComparer.OrdinalIgnoreCase) { "slate", "warm", "dark" };
    private static readonly HashSet<string> AllowedDateFormats = new(StringComparer.Ordinal) { "dd MMM yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd" };
    private static readonly HashSet<string> AllowedLandingPages = new(StringComparer.OrdinalIgnoreCase)
    {
        "/dashboard",
        "/transactions",
        "/accounts",
        "/budgets",
        "/goals",
        "/reports",
        "/recurring",
        "/settings"
    };

    public async Task<UserSettingsDto> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var settings = await EnsureSettingsAsync(userId, cancellationToken);
        var accountName = settings.DefaultAccountId.HasValue
            ? await dbContext.Accounts.AsNoTracking().Where(x => x.UserId == userId && x.Id == settings.DefaultAccountId.Value).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken)
            : null;

        return Map(user, settings, accountName);
    }

    public async Task<ProfileSettingsDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var email = NormalizeEmail(request.Email);
        ValidateName(request.FirstName, "First name");
        ValidateName(request.LastName, "Last name");

        var emailExists = await dbContext.Users.AnyAsync(x => x.Id != userId && x.Email == email, cancellationToken);
        if (emailExists)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = email;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ProfileSettingsDto(user.Id, user.Email, user.FirstName, user.LastName);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            throw new ValidationException("Current password is required.");
        }

        ValidatePassword(request.NewPassword);

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new ValidationException("Current password is incorrect.");
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PreferenceSettingsDto> UpdatePreferencesAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        var settings = await EnsureSettingsAsync(userId, cancellationToken);

        var currencyCode = NormalizeCurrency(request.PreferredCurrencyCode);
        if (!AllowedDateFormats.Contains(request.DateFormat))
        {
            throw new ValidationException("Selected date format is invalid.");
        }

        var landingPage = request.LandingPage.Trim();
        if (!AllowedLandingPages.Contains(landingPage))
        {
            throw new ValidationException("Selected landing page is invalid.");
        }

        var theme = request.Theme.Trim().ToLowerInvariant();
        if (!AllowedThemes.Contains(theme))
        {
            throw new ValidationException("Selected theme is invalid.");
        }

        settings.PreferredCurrencyCode = currencyCode;
        settings.DateFormat = request.DateFormat;
        settings.LandingPage = landingPage;
        settings.Theme = theme;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PreferenceSettingsDto(settings.PreferredCurrencyCode, settings.DateFormat, settings.LandingPage, settings.Theme);
    }

    public async Task<NotificationSettingsDto> UpdateNotificationsAsync(Guid userId, UpdateNotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = await EnsureSettingsAsync(userId, cancellationToken);
        settings.BudgetWarningsEnabled = request.BudgetWarningsEnabled;
        settings.GoalRemindersEnabled = request.GoalRemindersEnabled;
        settings.RecurringRemindersEnabled = request.RecurringRemindersEnabled;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new NotificationSettingsDto(settings.BudgetWarningsEnabled, settings.GoalRemindersEnabled, settings.RecurringRemindersEnabled);
    }

    public async Task<FinancialDefaultsSettingsDto> UpdateFinancialDefaultsAsync(Guid userId, UpdateFinancialDefaultsRequest request, CancellationToken cancellationToken)
    {
        var settings = await EnsureSettingsAsync(userId, cancellationToken);

        if (request.DefaultBudgetAlertThresholdPercent is < 1 or > 100)
        {
            throw new ValidationException("Default budget alert threshold must be between 1 and 100.");
        }

        Account? account = null;
        if (request.DefaultAccountId.HasValue)
        {
            account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == request.DefaultAccountId.Value && !x.IsArchived, cancellationToken)
                ?? throw new ValidationException("Selected default account is invalid or archived.");
        }

        settings.DefaultAccountId = account?.Id;
        settings.DefaultPaymentMethod = NormalizeNullable(request.DefaultPaymentMethod, 64);
        settings.DefaultBudgetAlertThresholdPercent = request.DefaultBudgetAlertThresholdPercent;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new FinancialDefaultsSettingsDto(settings.DefaultAccountId, account?.Name, settings.DefaultPaymentMethod, settings.DefaultBudgetAlertThresholdPercent);
    }

    public async Task<SampleDataSeedStatusDto> GetSampleDataSeedStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var transactionCount = await dbContext.Transactions.CountAsync(x => x.UserId == userId && !x.IsDeleted, cancellationToken);
        var activeAccountCount = await dbContext.Accounts.CountAsync(x => x.UserId == userId && !x.IsArchived, cancellationToken);
        var budgetCount = await dbContext.Budgets.CountAsync(x => x.UserId == userId, cancellationToken);
        var goalCount = await dbContext.Goals.CountAsync(x => x.UserId == userId && x.Status != GoalStatus.Archived, cancellationToken);
        var recurringRuleCount = await dbContext.RecurringTransactionRules.CountAsync(
            x => x.UserId == userId && x.Status != RecurringRuleStatus.Deleted,
            cancellationToken);

        var canRunSeed = transactionCount == 0;
        return new SampleDataSeedStatusDto(
            CanSeedFromDashboard: canRunSeed,
            CanRunSeed: canRunSeed,
            HasTransactions: transactionCount > 0,
            ActiveAccountCount: activeAccountCount,
            BudgetCount: budgetCount,
            GoalCount: goalCount,
            RecurringRuleCount: recurringRuleCount);
    }

    public async Task<SeedSampleDataResultDto> SeedSampleDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        var status = await GetSampleDataSeedStatusAsync(userId, cancellationToken);
        if (!status.CanRunSeed)
        {
            throw new ValidationException("Sample data can only be added before your first transaction is recorded.");
        }

        var settings = await EnsureSettingsAsync(userId, cancellationToken);
        await categorySeeder.EnsureDefaultsAsync(userId, cancellationToken);

        await using var seedTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var activeAccounts = await dbContext.Accounts
            .Where(x => x.UserId == userId && !x.IsArchived)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var preferredCurrency = string.IsNullOrWhiteSpace(settings.PreferredCurrencyCode) ? "INR" : settings.PreferredCurrencyCode.Trim().ToUpperInvariant();
        var seededAccounts = EnsureSeedAccounts(userId, preferredCurrency, activeAccounts);
        await dbContext.SaveChangesAsync(cancellationToken);

        var categories = await dbContext.Categories
            .Where(x => x.UserId == userId && !x.IsArchived)
            .ToListAsync(cancellationToken);

        var seedNow = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        var transactions = CreateSeedTransactions(userId, categories, seededAccounts, seedNow);
        dbContext.Transactions.AddRange(transactions);

        var budgetsCreated = await EnsureSampleBudgetsAsync(userId, categories, transactions, seedNow, cancellationToken);
        var goalsCreated = await EnsureSampleGoalAsync(userId, seededAccounts.Savings, seedNow, cancellationToken);
        var recurringRulesCreated = await EnsureSampleRecurringRulesAsync(userId, categories, seededAccounts.Primary, seedNow, cancellationToken);

        if (!settings.DefaultAccountId.HasValue)
        {
            settings.DefaultAccountId = seededAccounts.Primary.Id;
        }

        settings.DefaultPaymentMethod ??= "UPI";

        await dbContext.SaveChangesAsync(cancellationToken);
        await seedTransaction.CommitAsync(cancellationToken);

        var accountsCreated = seededAccounts.AccountsCreated;
        var transactionsCreated = transactions.Count;
        return new SeedSampleDataResultDto(
            $"Added a 3-month sample workspace with {transactionsCreated} transactions, {goalsCreated} goal{(goalsCreated == 1 ? string.Empty : "s")}, and {recurringRulesCreated} recurring rule{(recurringRulesCreated == 1 ? string.Empty : "s")}.",
            accountsCreated,
            transactionsCreated,
            budgetsCreated,
            goalsCreated,
            recurringRulesCreated);
    }

    public async Task LogoutAllSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        if (activeTokens.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedUtc = now;
            token.RevocationReason = "All sessions revoked by user";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserSettings> EnsureSettingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var settings = await dbContext.Set<UserSettings>()
            .Include(x => x.DefaultAccount)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new UserSettings
        {
            UserId = user.Id,
        };

        dbContext.Set<UserSettings>().Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static UserSettingsDto Map(User user, UserSettings settings, string? accountName)
        => new(
            new ProfileSettingsDto(user.Id, user.Email, user.FirstName, user.LastName),
            new PreferenceSettingsDto(settings.PreferredCurrencyCode, settings.DateFormat, settings.LandingPage, settings.Theme),
            new NotificationSettingsDto(settings.BudgetWarningsEnabled, settings.GoalRemindersEnabled, settings.RecurringRemindersEnabled),
            new FinancialDefaultsSettingsDto(settings.DefaultAccountId, accountName, settings.DefaultPaymentMethod, settings.DefaultBudgetAlertThresholdPercent));

    private static void ValidateName(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 100)
        {
            throw new ValidationException($"{fieldName} is required and must be 100 characters or fewer.");
        }
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ValidationException("Email is required.");
        }

        var normalized = email.Trim().ToLowerInvariant();
        if (normalized.Length > 256 || !normalized.Contains('@'))
        {
            throw new ValidationException("Email must be valid and 256 characters or fewer.");
        }

        return normalized;
    }

    private static string NormalizeCurrency(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length != 3)
        {
            throw new ValidationException("Preferred currency must be a 3-letter currency code.");
        }

        return value.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ValidationException($"Value must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 128)
        {
            throw new ValidationException("New password must be between 12 and 128 characters.");
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            throw new ValidationException("New password must include uppercase, lowercase, number, and special characters.");
        }
    }

    private SeedAccountsSelection EnsureSeedAccounts(Guid userId, string currencyCode, List<Account> activeAccounts)
    {
        var accountsCreated = 0;

        var primary = activeAccounts.FirstOrDefault(x => x.Type is AccountType.BankAccount or AccountType.CashWallet or AccountType.SavingsAccount);
        if (primary is null || primary.Type == AccountType.CreditCard)
        {
            primary = CreateAccount(userId, "Household Checking", AccountType.BankAccount, currencyCode, 24000m, "Ledger Nest Bank", "4821");
            dbContext.Accounts.Add(primary);
            activeAccounts.Add(primary);
            accountsCreated++;
        }

        var savings = activeAccounts.FirstOrDefault(x => x.Id != primary.Id && x.Type == AccountType.SavingsAccount);
        if (savings is null)
        {
            savings = CreateAccount(userId, "Rainy Day Savings", AccountType.SavingsAccount, primary.CurrencyCode, 18000m, "Ledger Nest Bank", "1044");
            dbContext.Accounts.Add(savings);
            activeAccounts.Add(savings);
            accountsCreated++;
        }

        var wallet = activeAccounts.FirstOrDefault(x => x.Id != primary.Id && x.Id != savings.Id && x.Type == AccountType.CashWallet);
        if (wallet is null)
        {
            wallet = CreateAccount(userId, "Daily Wallet", AccountType.CashWallet, primary.CurrencyCode, 2500m, null, null);
            dbContext.Accounts.Add(wallet);
            activeAccounts.Add(wallet);
            accountsCreated++;
        }

        return new SeedAccountsSelection(primary, savings, wallet, accountsCreated);
    }

    private List<Transaction> CreateSeedTransactions(Guid userId, List<Category> categories, SeedAccountsSelection accounts, DateTime nowUtc)
    {
        var transactions = new List<Transaction>();
        var salaryCategory = FindCategory(categories, "Salary", CategoryType.Income);
        var freelanceCategory = FindCategory(categories, "Freelance", CategoryType.Income);
        var investmentCategory = FindCategory(categories, "Investment", CategoryType.Income);
        var rentCategory = FindCategory(categories, "Rent", CategoryType.Expense);
        var utilitiesCategory = FindCategory(categories, "Utilities", CategoryType.Expense);
        var foodCategory = FindCategory(categories, "Food", CategoryType.Expense);
        var transportCategory = FindCategory(categories, "Transport", CategoryType.Expense);
        var subscriptionsCategory = FindCategory(categories, "Subscriptions", CategoryType.Expense);
        var entertainmentCategory = FindCategory(categories, "Entertainment", CategoryType.Expense);
        var shoppingCategory = FindCategory(categories, "Shopping", CategoryType.Expense);
        var healthCategory = FindCategory(categories, "Health", CategoryType.Expense);
        var educationCategory = FindCategory(categories, "Education", CategoryType.Expense);
        var miscellaneousCategory = FindCategory(categories, "Miscellaneous", CategoryType.Expense);

        var currentMonthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var monthOffset = -2; monthOffset <= 0; monthOffset++)
        {
            var monthStart = currentMonthStart.AddMonths(monthOffset);
            var progression = monthOffset + 2;

            AddIfPresent(BuildSeedDate(monthStart, 1, 9, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Income, 82000m, date, salaryCategory, "Primary salary deposit", "Asterix Systems", "Bank transfer"));
            AddIfPresent(BuildSeedDate(monthStart, 2, 10, nowUtc), date => AddTransfer(transactions, userId, accounts.Primary, accounts.Savings, 18000m, date, "Automatic move to savings bucket"));
            AddIfPresent(BuildSeedDate(monthStart, 3, 8, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Expense, 18500m, date, rentCategory, "Apartment rent", "Green Heights Residency", "Bank transfer"));
            AddIfPresent(BuildSeedDate(monthStart, 5, 19, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Expense, 3200m + (progression * 120m), date, foodCategory, "Weekly groceries", "Fresh Basket", "UPI"));
            AddIfPresent(BuildSeedDate(monthStart, 7, 11, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Expense, 2850m + (progression * 140m), date, utilitiesCategory, "Electricity, broadband, and water", "Utility Hub", "Auto debit"));
            AddIfPresent(BuildSeedDate(monthStart, 9, 18, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Expense, 1450m + (progression * 90m), date, transportCategory, "Fuel and metro recharge", "City Mobility", "Card"));
            AddIfPresent(BuildSeedDate(monthStart, 12, 20, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Expense, 1650m + (progression * 110m), date, foodCategory, "Dining out", "Urban Tiffin", "Card"));

            if (progression is 0 or 2)
            {
                AddIfPresent(BuildSeedDate(monthStart, 15, 14, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Income, 9000m + (progression * 1000m), date, freelanceCategory, "Consulting payout", "Northwind Studio", "Bank transfer"));
            }

            AddIfPresent(BuildSeedDate(monthStart, 16, 7, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Expense, 899m, date, subscriptionsCategory, "Streaming and cloud subscriptions", "Digital Services", "Auto debit"));
            AddIfPresent(BuildSeedDate(monthStart, 18, 21, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Expense, 2100m + (progression * 130m), date, entertainmentCategory, "Weekend leisure spending", "City Leisure", "Card"));
            AddIfPresent(BuildSeedDate(monthStart, 20, 10, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Income, 2500m, date, investmentCategory, "Dividend sweep", "Index Fund", "Bank transfer"));
            AddIfPresent(BuildSeedDate(monthStart, 22, 17, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Expense, 2200m + (progression * 180m), date, shoppingCategory, "Home and personal shopping", "Market Square", "UPI"));
            AddIfPresent(BuildSeedDate(monthStart, 24, 9, nowUtc), date => AddTransfer(transactions, userId, accounts.Primary, accounts.Wallet, 1800m, date, "Cash wallet top-up"));
            AddIfPresent(BuildSeedDate(monthStart, 25, 13, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Wallet, TransactionType.Expense, 320m + (progression * 20m), date, foodCategory, "Snacks and tea", "Neighbourhood Cafe", "Cash"));
            AddIfPresent(BuildSeedDate(monthStart, 26, 8, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Wallet, TransactionType.Expense, 220m + (progression * 15m), date, transportCategory, "Auto and parking", "Local commute", "Cash"));
            AddIfPresent(BuildSeedDate(monthStart, 26, 18, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Expense, 1200m, date, educationCategory, "Professional upskilling", "Skillforge", "UPI"));

            if (progression == 1)
            {
                AddIfPresent(BuildSeedDate(monthStart, 27, 16, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Primary, TransactionType.Expense, 1850m, date, healthCategory, "Annual health check and medicines", "Care Clinic", "Card"));
            }

            AddIfPresent(BuildSeedDate(monthStart, 28, 19, nowUtc), date => AddLedgerTransaction(transactions, userId, accounts.Wallet, TransactionType.Expense, 180m + (progression * 25m), date, miscellaneousCategory, "Daily incidentals", "Local store", "Cash"));
        }

        return transactions;
    }

    private async Task<int> EnsureSampleBudgetsAsync(Guid userId, List<Category> categories, List<Transaction> transactions, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var currentMonthExpenseByCategory = transactions
            .Where(x => x.Type == TransactionType.Expense && x.DateUtc.Year == nowUtc.Year && x.DateUtc.Month == nowUtc.Month && x.CategoryId.HasValue)
            .GroupBy(x => x.CategoryId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

        var existingCategoryIds = await dbContext.Budgets
            .Where(x => x.UserId == userId && x.Year == nowUtc.Year && x.Month == nowUtc.Month)
            .Select(x => x.CategoryId)
            .ToListAsync(cancellationToken);

        var templates = new[]
        {
            new BudgetSeedTemplate("Food", amount => Math.Max(amount + 950m, 7000m), 85),
            new BudgetSeedTemplate("Rent", amount => Math.Max(amount, 18500m), 95),
            new BudgetSeedTemplate("Utilities", amount => Math.Max(amount + 250m, 3200m), 85),
            new BudgetSeedTemplate("Transport", amount => Math.Max(amount + 450m, 2200m), 85),
            new BudgetSeedTemplate("Entertainment", amount => Math.Max(amount - 250m, 2000m), 80),
            new BudgetSeedTemplate("Shopping", amount => Math.Max(amount - 200m, 2400m), 80),
            new BudgetSeedTemplate("Subscriptions", amount => Math.Max(amount + 150m, 1100m), 90),
        };

        var created = 0;
        foreach (var template in templates)
        {
            var category = FindCategory(categories, template.CategoryName, CategoryType.Expense);
            if (existingCategoryIds.Contains(category.Id))
            {
                continue;
            }

            currentMonthExpenseByCategory.TryGetValue(category.Id, out var actualSpend);
            dbContext.Budgets.Add(new Budget
            {
                UserId = userId,
                CategoryId = category.Id,
                Year = nowUtc.Year,
                Month = nowUtc.Month,
                Amount = RoundMoney(template.ResolveAmount(actualSpend)),
                AlertThresholdPercent = template.AlertThresholdPercent
            });
            created++;
        }

        return created;
    }

    private async Task<int> EnsureSampleGoalAsync(Guid userId, Account savingsAccount, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var existingGoalCount = await dbContext.Goals.CountAsync(x => x.UserId == userId && x.Status != GoalStatus.Archived, cancellationToken);
        if (existingGoalCount > 0)
        {
            return 0;
        }

        var goal = new Goal
        {
            UserId = userId,
            Name = "Emergency Reserve",
            TargetAmount = 120000m,
            CurrentAmount = 0m,
            TargetDateUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(6),
            LinkedAccountId = savingsAccount.Id,
            Icon = "Shield",
            Color = "Forest",
            Status = GoalStatus.Active
        };

        dbContext.Goals.Add(goal);

        var contributionPlan = new[]
        {
            new { MonthOffset = -2, Day = 11, Amount = 9000m, Note = "Initial safety cushion" },
            new { MonthOffset = -1, Day = 11, Amount = 11000m, Note = "Bonus-backed contribution" },
            new { MonthOffset = 0, Day = 11, Amount = 13000m, Note = "Monthly reserve top-up" },
        };

        foreach (var item in contributionPlan)
        {
            var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(item.MonthOffset);
            var occurredAtUtc = BuildSeedDate(monthStart, item.Day, 12, nowUtc);
            if (!occurredAtUtc.HasValue)
            {
                continue;
            }

            goal.CurrentAmount = RoundMoney(goal.CurrentAmount + item.Amount);
            savingsAccount.CurrentBalance = RoundMoney(savingsAccount.CurrentBalance - item.Amount);
            dbContext.GoalEntries.Add(new GoalEntry
            {
                GoalId = goal.Id,
                UserId = userId,
                AccountId = savingsAccount.Id,
                Type = GoalEntryType.Contribution,
                Amount = item.Amount,
                GoalAmountAfterEntry = goal.CurrentAmount,
                Note = item.Note,
                OccurredAtUtc = occurredAtUtc.Value
            });
        }

        goal.Status = goal.CurrentAmount >= goal.TargetAmount ? GoalStatus.Completed : GoalStatus.Active;
        return 1;
    }

    private async Task<int> EnsureSampleRecurringRulesAsync(Guid userId, List<Category> categories, Account primaryAccount, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var existingRuleCount = await dbContext.RecurringTransactionRules.CountAsync(x => x.UserId == userId && x.Status != RecurringRuleStatus.Deleted, cancellationToken);
        if (existingRuleCount > 0)
        {
            return 0;
        }

        var rentCategory = FindCategory(categories, "Rent", CategoryType.Expense);
        var subscriptionsCategory = FindCategory(categories, "Subscriptions", CategoryType.Expense);
        var freelanceCategory = FindCategory(categories, "Freelance", CategoryType.Income);

        var rules = new[]
        {
            new RecurringTransactionRule
            {
                UserId = userId,
                Title = "Monthly rent reminder",
                Type = TransactionType.Expense,
                Amount = 18500m,
                CategoryId = rentCategory.Id,
                AccountId = primaryAccount.Id,
                Frequency = RecurringFrequency.Monthly,
                StartDateUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-2).AddDays(2),
                NextRunDateUtc = BuildFutureRecurringDate(nowUtc, 2),
                AutoCreateTransaction = false,
                Status = RecurringRuleStatus.Active
            },
            new RecurringTransactionRule
            {
                UserId = userId,
                Title = "Subscription bundle",
                Type = TransactionType.Expense,
                Amount = 899m,
                CategoryId = subscriptionsCategory.Id,
                AccountId = primaryAccount.Id,
                Frequency = RecurringFrequency.Monthly,
                StartDateUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-2).AddDays(15),
                NextRunDateUtc = BuildFutureRecurringDate(nowUtc, 5),
                AutoCreateTransaction = false,
                Status = RecurringRuleStatus.Active
            },
            new RecurringTransactionRule
            {
                UserId = userId,
                Title = "Weekly consulting payout",
                Type = TransactionType.Income,
                Amount = 3000m,
                CategoryId = freelanceCategory.Id,
                AccountId = primaryAccount.Id,
                Frequency = RecurringFrequency.Weekly,
                StartDateUtc = nowUtc.Date.AddDays(-21),
                NextRunDateUtc = BuildFutureRecurringDate(nowUtc, 3),
                AutoCreateTransaction = false,
                Status = RecurringRuleStatus.Active
            }
        };

        dbContext.RecurringTransactionRules.AddRange(rules);
        return rules.Length;
    }

    private static Account CreateAccount(Guid userId, string name, AccountType type, string currencyCode, decimal openingBalance, string? institutionName, string? last4Digits)
        => new()
        {
            UserId = userId,
            Name = name,
            Type = type,
            CurrencyCode = currencyCode,
            OpeningBalance = openingBalance,
            CurrentBalance = openingBalance,
            InstitutionName = institutionName,
            Last4Digits = last4Digits
        };

    private static Category FindCategory(IEnumerable<Category> categories, string name, CategoryType type)
        => categories.First(x => x.Type == type && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    private static DateTime? BuildSeedDate(DateTime monthStartUtc, int day, int hour, DateTime nowUtc)
    {
        var actualDay = Math.Min(day, DateTime.DaysInMonth(monthStartUtc.Year, monthStartUtc.Month));
        if (monthStartUtc.Year == nowUtc.Year && monthStartUtc.Month == nowUtc.Month && actualDay > nowUtc.Day)
        {
            return null;
        }

        return new DateTime(monthStartUtc.Year, monthStartUtc.Month, actualDay, hour, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime BuildFutureRecurringDate(DateTime nowUtc, int daysAhead)
    {
        var candidate = nowUtc.Date.AddDays(daysAhead).AddHours(18);
        var monthEnd = new DateTime(nowUtc.Year, nowUtc.Month, DateTime.DaysInMonth(nowUtc.Year, nowUtc.Month), 20, 0, 0, DateTimeKind.Utc);
        return candidate <= monthEnd ? candidate : monthEnd;
    }

    private static void AddIfPresent(DateTime? occurredAtUtc, Action<DateTime> add)
    {
        if (occurredAtUtc.HasValue)
        {
            add(occurredAtUtc.Value);
        }
    }

    private static void AddLedgerTransaction(
        ICollection<Transaction> transactions,
        Guid userId,
        Account account,
        TransactionType type,
        decimal amount,
        DateTime occurredAtUtc,
        Category category,
        string note,
        string merchant,
        string paymentMethod)
    {
        amount = RoundMoney(amount);
        account.CurrentBalance = type switch
        {
            TransactionType.Income => RoundMoney(account.CurrentBalance + amount),
            TransactionType.Expense => RoundMoney(account.CurrentBalance - amount),
            _ => account.CurrentBalance
        };

        transactions.Add(new Transaction
        {
            UserId = userId,
            AccountId = account.Id,
            Type = type,
            Amount = amount,
            DateUtc = occurredAtUtc,
            CategoryId = category.Id,
            Note = note,
            Merchant = merchant,
            PaymentMethod = paymentMethod,
            CreatedByUserId = userId,
            UpdatedByUserId = userId
        });
    }

    private static void AddTransfer(
        ICollection<Transaction> transactions,
        Guid userId,
        Account fromAccount,
        Account toAccount,
        decimal amount,
        DateTime occurredAtUtc,
        string note)
    {
        amount = RoundMoney(amount);
        fromAccount.CurrentBalance = RoundMoney(fromAccount.CurrentBalance - amount);
        toAccount.CurrentBalance = RoundMoney(toAccount.CurrentBalance + amount);

        transactions.Add(new Transaction
        {
            UserId = userId,
            AccountId = fromAccount.Id,
            TransferAccountId = toAccount.Id,
            Type = TransactionType.Transfer,
            Amount = amount,
            DateUtc = occurredAtUtc,
            Note = note,
            PaymentMethod = "Internal transfer",
            CreatedByUserId = userId,
            UpdatedByUserId = userId
        });
    }

    private static decimal RoundMoney(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record SeedAccountsSelection(Account Primary, Account Savings, Account Wallet, int AccountsCreated);
    private sealed record BudgetSeedTemplate(string CategoryName, Func<decimal, decimal> ResolveAmount, int AlertThresholdPercent);
}
