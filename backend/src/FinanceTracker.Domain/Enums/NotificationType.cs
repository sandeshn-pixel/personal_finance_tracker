namespace FinanceTracker.Domain.Enums;

public enum NotificationType
{
    RecurringDueReminder = 1,
    RecurringExecutionFailed = 2,
    GoalTargetApproaching = 3,
    GoalCompleted = 4,
    RuleTriggeredAlert = 5,
    SharedAccountInvite = 6
}
