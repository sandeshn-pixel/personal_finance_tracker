using FinanceTracker.Application.Accounts.Interfaces;
using FinanceTracker.Application.Automation.Interfaces;
using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Budgets.Interfaces;
using FinanceTracker.Application.Categories.Interfaces;
using FinanceTracker.Application.Dashboard.Interfaces;
using FinanceTracker.Application.Exports.Interfaces;
using FinanceTracker.Application.Forecasting.Interfaces;
using FinanceTracker.Application.Goals.Interfaces;
using FinanceTracker.Application.Insights.Interfaces;
using FinanceTracker.Application.Notifications.Interfaces;
using FinanceTracker.Application.RecurringTransactions.Interfaces;
using FinanceTracker.Application.Reports.Interfaces;
using FinanceTracker.Application.Rules.Interfaces;
using FinanceTracker.Application.Settings.Interfaces;
using FinanceTracker.Application.Transactions.Interfaces;
using FinanceTracker.Infrastructure.Auth;
using FinanceTracker.Infrastructure.Automation;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Forecasting;
using FinanceTracker.Infrastructure.Insights;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Infrastructure.Reporting;
using FinanceTracker.Infrastructure.Rules;
using FinanceTracker.Infrastructure.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenGenerator, TokenGenerator>();
        services.AddScoped<IPasswordResetEmailSender, PasswordResetEmailSender>();
        services.AddScoped<IPasswordHasher<Domain.Entities.User>, PasswordHasher<Domain.Entities.User>>();
        services.AddScoped<ICategorySeeder, CategorySeeder>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IRecurringTransactionService, RecurringTransactionService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<IAutomationStatusTracker, AutomationStatusTracker>();
        services.AddScoped<IAutomationService, AutomationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IForecastService, ForecastService>();
        services.AddScoped<IHealthScoreService, HealthScoreService>();
        services.AddScoped<IRuleService, RuleService>();
        services.AddScoped<ITransactionRuleEvaluator, TransactionRuleEvaluator>();
        services.AddScoped<ISettingsService, SettingsService>();

        return services;
    }
}

