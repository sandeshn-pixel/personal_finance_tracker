using FinanceTracker.Api.Controllers;
using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Automation.DTOs;
using FinanceTracker.Application.Automation.Interfaces;
using FinanceTracker.Application.RecurringTransactions.DTOs;
using FinanceTracker.Application.RecurringTransactions.Interfaces;
using FinanceTracker.Application.RecurringTransactions.Validators;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Automation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Backend.Tests;

public sealed class ControllerBehaviorTests
{
    [Fact]
    public void AutomationStatus_ReturnsSnapshot_ForAuthenticatedUser()
    {
        var summary = new AutomationRunSummaryDto(2, 3, 3, 1, 0, 1, 2, new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc));
        var tracker = new FakeAutomationStatusTracker(new AutomationStatusDto(
            true,
            60,
            new DateTime(2026, 3, 18, 9, 59, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc),
            true,
            false,
            0,
            1,
            new DateTime(2026, 3, 18, 10, 1, 0, DateTimeKind.Utc),
            null,
            summary));
        var controller = new AutomationController(
            tracker,
            new FakeCurrentUserService(Guid.NewGuid()),
            new FakeOptionsMonitor<AutomationOptions>(new AutomationOptions { EnableBackgroundProcessing = true, PollingIntervalSeconds = 60 }));

        var result = controller.Status();

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AutomationStatusDto>(ok.Value);
        Assert.True(payload.BackgroundProcessingEnabled);
        Assert.Equal(60, payload.PollingIntervalSeconds);
        Assert.Equal(1, payload.TotalFailureCount);
        Assert.NotNull(payload.LastSummary);
        Assert.Equal(1, payload.LastSummary!.AutoOccurrencesDeferredForRetry);
    }

    [Fact]
    public async Task RecurringTransactions_Create_InvalidRequest_ReturnsValidationProblem()
    {
        var service = new FakeRecurringTransactionService();
        var controller = new RecurringTransactionsController(
            service,
            new FakeCurrentUserService(Guid.NewGuid()),
            new CreateRecurringTransactionRequestValidator(),
            new UpdateRecurringTransactionRequestValidator());

        var response = await controller.Create(new CreateRecurringTransactionRequest
        {
            Title = string.Empty,
            Type = TransactionType.Expense,
            Amount = 0m,
            AccountId = Guid.NewGuid(),
            Frequency = RecurringFrequency.Monthly,
            StartDateUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local)
        }, CancellationToken.None);

        var validationProblem = Assert.IsType<ObjectResult>(response);
        var problemDetails = Assert.IsType<ValidationProblemDetails>(validationProblem.Value);
        Assert.True(problemDetails.Errors.Count > 0);
        Assert.False(service.CreateCalled);
    }

    [Fact]
    public async Task RecurringTransactions_ProcessDue_ReturnsSummaryForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var expectedSummary = new RecurringExecutionSummaryDto(1, 1, 1, 0, 0, 0, new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc));
        var service = new FakeRecurringTransactionService
        {
            ProcessDueResult = expectedSummary
        };
        var controller = new RecurringTransactionsController(
            service,
            new FakeCurrentUserService(userId),
            new CreateRecurringTransactionRequestValidator(),
            new UpdateRecurringTransactionRequestValidator());

        var response = await controller.ProcessDue(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<RecurringExecutionSummaryDto>(ok.Value);
        Assert.Equal(expectedSummary, payload);
        Assert.Equal(userId, service.LastProcessedUserId);
    }

    private sealed class FakeCurrentUserService(Guid? userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class FakeOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue => currentValue;
        public T Get(string? name) => currentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class FakeAutomationStatusTracker(AutomationStatusDto snapshot) : IAutomationStatusTracker
    {
        public void RecordStarted(DateTime startedUtc)
        {
        }

        public void RecordSucceeded(AutomationRunSummaryDto summary, DateTime completedUtc, DateTime nextAttemptUtc)
        {
        }

        public int RecordFailed(DateTime completedUtc, string errorMessage, DateTime nextAttemptUtc) => 1;

        public AutomationStatusDto GetSnapshot(bool backgroundProcessingEnabled, int pollingIntervalSeconds)
            => snapshot with
            {
                BackgroundProcessingEnabled = backgroundProcessingEnabled,
                PollingIntervalSeconds = pollingIntervalSeconds
            };
    }

    private sealed class FakeRecurringTransactionService : IRecurringTransactionService
    {
        public bool CreateCalled { get; private set; }
        public Guid? LastProcessedUserId { get; private set; }
        public RecurringExecutionSummaryDto ProcessDueResult { get; set; } = new(0, 0, 0, 0, 0, 0, DateTime.UtcNow);

        public Task<IReadOnlyCollection<RecurringTransactionDto>> ListAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<RecurringTransactionDto>>([]);

        public Task<RecurringTransactionDto?> GetAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken)
            => Task.FromResult<RecurringTransactionDto?>(null);

        public Task<RecurringTransactionDto> CreateAsync(Guid userId, CreateRecurringTransactionRequest request, CancellationToken cancellationToken)
        {
            CreateCalled = true;
            return Task.FromResult(new RecurringTransactionDto(
                Guid.NewGuid(),
                request.Title,
                request.Type,
                request.Amount,
                request.AccountId,
                "Primary",
                request.TransferAccountId,
                null,
                request.CategoryId,
                null,
                request.Frequency,
                request.StartDateUtc,
                request.EndDateUtc,
                request.StartDateUtc,
                request.AutoCreateTransaction,
                RecurringRuleStatus.Active,
                true,
                DateTime.UtcNow,
                DateTime.UtcNow,
                null));
        }

        public Task<RecurringTransactionDto> UpdateAsync(Guid userId, Guid ruleId, UpdateRecurringTransactionRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<RecurringTransactionDto> PauseAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<RecurringTransactionDto> ResumeAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task DeleteAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<RecurringExecutionSummaryDto> ProcessDueAsync(Guid userId, DateTime asOfUtc, CancellationToken cancellationToken)
        {
            LastProcessedUserId = userId;
            return Task.FromResult(ProcessDueResult);
        }
    }
}
