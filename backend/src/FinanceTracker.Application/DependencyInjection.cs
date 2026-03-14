using FluentValidation;
using FinanceTracker.Application.Accounts.DTOs;
using FinanceTracker.Application.Accounts.Validators;
using FinanceTracker.Application.Auth.DTOs;
using FinanceTracker.Application.Auth.Validators;
using FinanceTracker.Application.Budgets.DTOs;
using FinanceTracker.Application.Budgets.Validators;
using FinanceTracker.Application.Categories.DTOs;
using FinanceTracker.Application.Categories.Validators;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Application.Reports.Validators;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Application.Transactions.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<CreateAccountRequest>, CreateAccountRequestValidator>();
        services.AddScoped<IValidator<UpdateAccountRequest>, UpdateAccountRequestValidator>();
        services.AddScoped<IValidator<CreateCategoryRequest>, CreateCategoryRequestValidator>();
        services.AddScoped<IValidator<UpdateCategoryRequest>, UpdateCategoryRequestValidator>();
        services.AddScoped<IValidator<UpsertTransactionRequest>, UpsertTransactionRequestValidator>();
        services.AddScoped<IValidator<TransactionListQuery>, TransactionListQueryValidator>();
        services.AddScoped<IValidator<CreateBudgetRequest>, CreateBudgetRequestValidator>();
        services.AddScoped<IValidator<UpdateBudgetRequest>, UpdateBudgetRequestValidator>();
        services.AddScoped<IValidator<BudgetMonthQuery>, BudgetMonthQueryValidator>();
        services.AddScoped<IValidator<CopyBudgetsRequest>, CopyBudgetsRequestValidator>();
        services.AddScoped<IValidator<ReportQuery>, ReportQueryValidator>();
        return services;
    }
}
