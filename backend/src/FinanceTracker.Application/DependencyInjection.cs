using FinanceTracker.Application.Accounts.DTOs;
using FinanceTracker.Application.Accounts.Validators;
using FinanceTracker.Application.Auth.DTOs;
using FinanceTracker.Application.Auth.Validators;
using FinanceTracker.Application.Budgets.DTOs;
using FinanceTracker.Application.Budgets.Validators;
using FinanceTracker.Application.Categories.DTOs;
using FinanceTracker.Application.Categories.Validators;
using FinanceTracker.Application.Dashboard.DTOs;
using FinanceTracker.Application.Dashboard.Validators;
using FinanceTracker.Application.Forecasting.DTOs;
using FinanceTracker.Application.Forecasting.Validators;
using FinanceTracker.Application.Goals.DTOs;
using FinanceTracker.Application.Goals.Validators;
using FinanceTracker.Application.Insights.DTOs;
using FinanceTracker.Application.Insights.Validators;
using FinanceTracker.Application.RecurringTransactions.DTOs;
using FinanceTracker.Application.RecurringTransactions.Validators;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Application.Reports.Validators;
using FinanceTracker.Application.Rules.DTOs;
using FinanceTracker.Application.Rules.Validators;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Application.Transactions.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>();
        services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();
        services.AddScoped<IValidator<CreateAccountRequest>, CreateAccountRequestValidator>();
        services.AddScoped<IValidator<UpdateAccountRequest>, UpdateAccountRequestValidator>();
        services.AddScoped<IValidator<InviteAccountMemberRequest>, InviteAccountMemberRequestValidator>();
        services.AddScoped<IValidator<AcceptAccountInviteRequest>, AcceptAccountInviteRequestValidator>();
        services.AddScoped<IValidator<UpdateAccountMemberRequest>, UpdateAccountMemberRequestValidator>();
        services.AddScoped<IValidator<CreateCategoryRequest>, CreateCategoryRequestValidator>();
        services.AddScoped<IValidator<UpdateCategoryRequest>, UpdateCategoryRequestValidator>();
        services.AddScoped<IValidator<UpsertTransactionRequest>, UpsertTransactionRequestValidator>();
        services.AddScoped<IValidator<TransactionListQuery>, TransactionListQueryValidator>();
        services.AddScoped<IValidator<CreateBudgetRequest>, CreateBudgetRequestValidator>();
        services.AddScoped<IValidator<UpdateBudgetRequest>, UpdateBudgetRequestValidator>();
        services.AddScoped<IValidator<BudgetMonthQuery>, BudgetMonthQueryValidator>();
        services.AddScoped<IValidator<CopyBudgetsRequest>, CopyBudgetsRequestValidator>();
        services.AddScoped<IValidator<ReportQuery>, ReportQueryValidator>();
        services.AddScoped<IValidator<DashboardQuery>, DashboardQueryValidator>();
        services.AddScoped<IValidator<ForecastQuery>, ForecastQueryValidator>();
        services.AddScoped<IValidator<HealthScoreQuery>, HealthScoreQueryValidator>();
        services.AddScoped<IValidator<UpsertTransactionRuleRequest>, UpsertTransactionRuleRequestValidator>();
        services.AddScoped<IValidator<CreateGoalRequest>, CreateGoalRequestValidator>();
        services.AddScoped<IValidator<UpdateGoalRequest>, UpdateGoalRequestValidator>();
        services.AddScoped<IValidator<RecordGoalEntryRequest>, RecordGoalEntryRequestValidator>();
        services.AddScoped<IValidator<CreateRecurringTransactionRequest>, CreateRecurringTransactionRequestValidator>();
        services.AddScoped<IValidator<UpdateRecurringTransactionRequest>, UpdateRecurringTransactionRequestValidator>();
        return services;
    }
}
