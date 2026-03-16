namespace FinanceTracker.Infrastructure.Automation;

public sealed class AutomationOptions
{
    public const string SectionName = "Automation";

    public bool EnableBackgroundProcessing { get; set; } = true;
    public int PollingIntervalSeconds { get; set; } = 60;
    public int GoalReminderLookaheadDays { get; set; } = 7;
}