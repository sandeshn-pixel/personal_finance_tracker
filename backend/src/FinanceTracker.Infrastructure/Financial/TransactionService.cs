using FinanceTracker.Application.Common;
using FinanceTracker.Application.Categories.Interfaces;
using FinanceTracker.Application.Rules.Interfaces;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Application.Transactions.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class TransactionService(
    ApplicationDbContext dbContext,
    ICategorySeeder categorySeeder,
    AccountAccessService accountAccessService,
    ITransactionRuleEvaluator? transactionRuleEvaluator = null) : ITransactionService
{
    public async Task<PagedResult<TransactionDto>> ListAsync(Guid userId, TransactionListQuery query, CancellationToken cancellationToken)
    {
        var transactions = dbContext.Transactions
            .AsNoTracking()
            .ApplyFilters(userId, query)
            .Include(x => x.Account)
            .Include(x => x.TransferAccount)
            .Include(x => x.Category)
            .Include(x => x.Tags)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .AsQueryable();

        var totalCount = await transactions.CountAsync(cancellationToken);
        var items = await transactions
            .OrderByDescending(x => x.DateUtc)
            .ThenByDescending(x => x.CreatedUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ProjectToDto()
            .ToListAsync(cancellationToken);

        return new PagedResult<TransactionDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<TransactionDto?> GetAsync(Guid userId, Guid transactionId, CancellationToken cancellationToken)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .WhereUserCanView(userId)
            .Where(x => x.Id == transactionId)
            .Include(x => x.Account)
            .Include(x => x.TransferAccount)
            .Include(x => x.Category)
            .Include(x => x.Tags)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
            .ProjectToDto()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TransactionDto> CreateAsync(Guid userId, UpsertTransactionRequest request, CancellationToken cancellationToken)
    {
        await categorySeeder.EnsureDefaultsAsync(userId, cancellationToken);
        await using var dbTransaction = await TransactionMapping.BeginFinancialTransactionAsync(dbContext, cancellationToken);

        var evaluated = transactionRuleEvaluator is null
            ? new RuleEvaluationResult(request, [], [])
            : await transactionRuleEvaluator.EvaluateAsync(userId, request, cancellationToken);

        var resolved = await ResolveReferencesAsync(userId, evaluated.Request, cancellationToken);

        var transaction = new Transaction
        {
            UserId = userId,
            AccountId = resolved.SourceAccount.Id,
            TransferAccountId = resolved.TransferAccount?.Id,
            Type = evaluated.Request.Type,
            Amount = decimal.Round(evaluated.Request.Amount, 2, MidpointRounding.AwayFromZero),
            DateUtc = DateTime.SpecifyKind(evaluated.Request.DateUtc, DateTimeKind.Utc),
            CategoryId = resolved.Category?.Id,
            Note = evaluated.Request.Note?.Trim(),
            Merchant = evaluated.Request.Merchant?.Trim(),
            PaymentMethod = evaluated.Request.PaymentMethod?.Trim(),
            RecurringTransactionId = evaluated.Request.RecurringTransactionId,
            CreatedByUserId = userId,
            UpdatedByUserId = userId
        };

        TransactionMapping.ApplyImpact(transaction.Type, transaction.Amount, resolved.SourceAccount, resolved.TransferAccount);
        transaction.Tags = TransactionMapping.NormalizeTags(evaluated.Request.Tags).Select(x => new TransactionTag { Value = x }).ToList();

        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (transactionRuleEvaluator is not null && evaluated.Alerts.Count > 0)
        {
            await transactionRuleEvaluator.PublishAlertsAsync(userId, transaction.Id, evaluated.Alerts, cancellationToken);
        }

        await dbTransaction.CommitAsync(cancellationToken);

        return await GetAsync(userId, transaction.Id, cancellationToken) ?? throw new NotFoundException("Transaction was not found after creation.");
    }

    public async Task<TransactionDto> UpdateAsync(Guid userId, Guid transactionId, UpsertTransactionRequest request, CancellationToken cancellationToken)
    {
        await categorySeeder.EnsureDefaultsAsync(userId, cancellationToken);
        await using var dbTransaction = await TransactionMapping.BeginFinancialTransactionAsync(dbContext, cancellationToken);

        var existing = await dbContext.Transactions
            .Include(x => x.Tags)
            .WhereUserCanView(userId)
            .SingleOrDefaultAsync(x => x.Id == transactionId, cancellationToken)
            ?? throw new NotFoundException("Transaction was not found.");

        var oldSource = await accountAccessService.FindAccessibleAccountAsync(userId, existing.AccountId, AccountMemberRole.Editor, includeArchived: true, cancellationToken)
            ?? throw new ForbiddenException("You do not have permission to change this transaction.");
        var oldTransfer = existing.TransferAccountId.HasValue
            ? await accountAccessService.FindAccessibleAccountAsync(userId, existing.TransferAccountId.Value, AccountMemberRole.Editor, includeArchived: true, cancellationToken)
                ?? throw new ForbiddenException("You do not have permission to change this transaction.")
            : null;

        TransactionMapping.ReverseImpact(existing, oldSource, oldTransfer);

        var resolved = await ResolveReferencesAsync(userId, request, cancellationToken);

        existing.AccountId = resolved.SourceAccount.Id;
        existing.TransferAccountId = resolved.TransferAccount?.Id;
        existing.Type = request.Type;
        existing.Amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        existing.DateUtc = DateTime.SpecifyKind(request.DateUtc, DateTimeKind.Utc);
        existing.CategoryId = resolved.Category?.Id;
        existing.Note = request.Note?.Trim();
        existing.Merchant = request.Merchant?.Trim();
        existing.PaymentMethod = request.PaymentMethod?.Trim();
        existing.RecurringTransactionId = request.RecurringTransactionId;
        existing.UpdatedByUserId = userId;

        TransactionMapping.ApplyImpact(existing.Type, existing.Amount, resolved.SourceAccount, resolved.TransferAccount);

        dbContext.TransactionTags.RemoveRange(existing.Tags);
        existing.Tags = TransactionMapping.NormalizeTags(request.Tags).Select(x => new TransactionTag { Value = x, TransactionId = existing.Id }).ToList();

        await dbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return await GetAsync(userId, existing.Id, cancellationToken) ?? throw new NotFoundException("Transaction was not found after update.");
    }

    public async Task DeleteAsync(Guid userId, Guid transactionId, CancellationToken cancellationToken)
    {
        await using var dbTransaction = await TransactionMapping.BeginFinancialTransactionAsync(dbContext, cancellationToken);

        var transaction = await dbContext.Transactions
            .Include(x => x.Tags)
            .WhereUserCanView(userId)
            .SingleOrDefaultAsync(x => x.Id == transactionId, cancellationToken)
            ?? throw new NotFoundException("Transaction was not found.");

        var source = await accountAccessService.FindAccessibleAccountAsync(userId, transaction.AccountId, AccountMemberRole.Editor, includeArchived: true, cancellationToken)
            ?? throw new ForbiddenException("You do not have permission to delete this transaction.");
        var transfer = transaction.TransferAccountId.HasValue
            ? await accountAccessService.FindAccessibleAccountAsync(userId, transaction.TransferAccountId.Value, AccountMemberRole.Editor, includeArchived: true, cancellationToken)
                ?? throw new ForbiddenException("You do not have permission to delete this transaction.")
            : null;

        TransactionMapping.ReverseImpact(transaction, source, transfer);
        transaction.IsDeleted = true;
        transaction.UpdatedByUserId = userId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
    }

    private async Task<(Account SourceAccount, Account? TransferAccount, Category? Category)> ResolveReferencesAsync(Guid userId, UpsertTransactionRequest request, CancellationToken cancellationToken)
    {
        var sourceAccount = await accountAccessService.FindAccessibleAccountAsync(userId, request.AccountId, AccountMemberRole.Editor, includeArchived: false, cancellationToken)
            ?? throw new ValidationException("Selected source account is invalid, inaccessible, or archived.");

        Account? transferAccount = null;
        Category? category = null;

        if (request.Type == TransactionType.Transfer)
        {
            if (!request.TransferAccountId.HasValue)
            {
                throw new ValidationException("Transfers require a destination account.");
            }

            if (request.TransferAccountId.Value == request.AccountId)
            {
                throw new ValidationException("Transfer source and destination accounts must be different.");
            }

            transferAccount = await accountAccessService.FindAccessibleAccountAsync(userId, request.TransferAccountId.Value, AccountMemberRole.Editor, includeArchived: false, cancellationToken)
                ?? throw new ValidationException("Selected destination account is invalid, inaccessible, or archived.");

            if (request.CategoryId.HasValue)
            {
                throw new ValidationException("Transfers must not include a category.");
            }
        }
        else
        {
            if (!request.CategoryId.HasValue)
            {
                throw new ValidationException("Income and expense transactions require a category.");
            }

            category = await dbContext.Categories.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == request.CategoryId.Value && !x.IsArchived, cancellationToken)
                ?? throw new ValidationException("Selected category is invalid or archived.");

            var expected = request.Type == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
            if (category.Type != expected)
            {
                throw new ValidationException("Category type does not match the selected transaction type.");
            }
        }

        return (sourceAccount, transferAccount, category);
    }
}
