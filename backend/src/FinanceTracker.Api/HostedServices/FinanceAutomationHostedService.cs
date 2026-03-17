using FinanceTracker.Application.Automation.Interfaces;
using FinanceTracker.Infrastructure.Automation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.HostedServices;

public sealed class FinanceAutomationHostedService(
    IServiceScopeFactory scopeFactory,
    IAutomationStatusTracker statusTracker,
    IOptionsMonitor<AutomationOptions> optionsMonitor,
    ILogger<FinanceAutomationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = optionsMonitor.CurrentValue;
            var delay = TimeSpan.FromSeconds(Math.Max(options.PollingIntervalSeconds, 15));

            if (options.EnableBackgroundProcessing)
            {
                var startedUtc = DateTime.UtcNow;
                statusTracker.RecordStarted(startedUtc);

                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
                    var summary = await automationService.RunAsync(startedUtc, stoppingToken);
                    statusTracker.RecordSucceeded(summary, DateTime.UtcNow);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    statusTracker.RecordFailed(DateTime.UtcNow, ex.Message);
                    logger.LogError(ex, "Automation cycle failed.");
                }
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
