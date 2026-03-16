import { useEffect, useMemo, useState } from "react";
import { useAuth } from "../../../app/providers/AuthProvider";
import { accountsApi } from "../../accounts/api/accountsApi";
import { dashboardApi, type DashboardSummaryDto } from "../api/dashboardApi";
import { Alert } from "../../../shared/components/Alert";
import { EmptyState } from "../../../shared/components/EmptyState";
import { PageLoader } from "../../../shared/components/PageLoader";
import { ProgressBar } from "../../../shared/components/ProgressBar";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { StatCard } from "../../../shared/components/StatCard";
import { ApiError } from "../../../shared/lib/api/client";
import { formatCurrency, formatDate } from "../../../shared/lib/format";

const goalBadges: Record<string, string> = {
  Shield: "S",
  Home: "H",
  Plane: "T",
  Book: "E",
  Car: "C",
  Gift: "G",
};

export function DashboardPage() {
  const { accessToken } = useAuth();
  const [summary, setSummary] = useState<DashboardSummaryDto | null>(null);
  const [accountCount, setAccountCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => { void load(); }, [accessToken]);

  async function load() {
    if (!accessToken) return;
    setLoading(true);
    try {
      const [summaryResponse, accounts] = await Promise.all([dashboardApi.summary(accessToken), accountsApi.list(accessToken)]);
      setSummary(summaryResponse);
      setAccountCount(accounts.filter((item) => !item.isArchived).length);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load dashboard.");
    } finally {
      setLoading(false);
    }
  }

  const maxSpend = useMemo(() => Math.max(...(summary?.spendingByCategory.map((item) => item.amount) ?? [0])), [summary]);
  const maxTrendValue = useMemo(() => Math.max(...(summary?.incomeExpenseTrend.flatMap((item) => [item.income, item.expense]) ?? [0])), [summary]);
  const maxAccountBalance = useMemo(() => Math.max(...(summary?.accountBalanceDistribution.map((item) => item.currentBalance) ?? [0])), [summary]);
  const budgetUsage = summary?.budgetHealth.totalBudgeted
    ? (summary.budgetHealth.totalSpent / summary.budgetHealth.totalBudgeted) * 100
    : 0;

  if (loading) return <PageLoader label="Loading dashboard" />;
  if (!summary) return <Alert message={errorMessage ?? "Dashboard is unavailable."} />;

  return (
    <div className="page-stack">
      <SectionHeader title="Dashboard" description="Server-calculated income, expense, account position, budget health, goal progress, and recent activity." />
      {errorMessage ? <Alert message={errorMessage} /> : null}
      <div className="stats-grid stats-grid--four">
        <StatCard label="Current month income" value={formatCurrency(summary.currentMonthIncome)} hint="Income transactions in the current month." tone="positive" />
        <StatCard label="Current month expense" value={formatCurrency(summary.currentMonthExpense)} hint="Expense transactions only. Goal movements are tracked separately." tone="negative" />
        <StatCard label="Net balance" value={formatCurrency(summary.netBalance)} hint={`${accountCount} active accounts included.`} />
        <StatCard label="Budget remaining" value={formatCurrency(summary.budgetHealth.totalRemaining)} hint={`${summary.budgetHealth.overBudgetCount} over budget, ${summary.budgetHealth.thresholdReachedCount} warnings.`} tone={summary.budgetHealth.totalRemaining < 0 ? "negative" : "positive"} />
      </div>
      <div className="stats-grid">
        <StatCard label="Saved to goals" value={formatCurrency(summary.savingsAutomation.totalContributedToGoals)} hint={`${summary.savingsAutomation.activeGoalsCount} active goals.`} tone="positive" />
        <StatCard label="Withdrawn from goals" value={formatCurrency(summary.savingsAutomation.totalWithdrawnFromGoals)} hint={`${summary.savingsAutomation.completedGoalsCount} completed goals.`} />
        <StatCard label="Recurring rules" value={String(summary.savingsAutomation.activeRecurringRulesCount)} hint={`${summary.savingsAutomation.dueRecurringRulesCount} due, ${summary.savingsAutomation.pausedRecurringRulesCount} paused.`} />
      </div>
      <div className="dashboard-grid">
        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Spending by category</h3>
            <p>Current month expense distribution.</p>
          </div>
          {summary.spendingByCategory.length === 0 ? (
            <EmptyState title="No expense activity" description="The chart appears once expense transactions are recorded." />
          ) : (
            <div className="chart-list">
              {summary.spendingByCategory.map((item) => (
                <div key={item.categoryId} className="chart-row">
                  <div className="chart-row__label">
                    <span>{item.categoryName}</span>
                    <strong>{formatCurrency(item.amount)}</strong>
                  </div>
                  <div className="chart-bar">
                    <div className="chart-bar__fill" style={{ width: `${(item.amount / (maxSpend || 1)) * 100}%` }} />
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Income vs expense trend</h3>
            <p>Weekly buckets for the current month.</p>
          </div>
          {summary.incomeExpenseTrend.length === 0 ? (
            <EmptyState title="No trend yet" description="Add income and expense activity to see a month-to-date trend." />
          ) : (
            <div className="trend-chart">
              {summary.incomeExpenseTrend.map((point) => (
                <div key={point.label} className="trend-chart__group">
                  <div className="trend-chart__bars">
                    <div className="trend-chart__bar trend-chart__bar--income" style={{ height: `${(point.income / (maxTrendValue || 1)) * 100}%` }} />
                    <div className="trend-chart__bar trend-chart__bar--expense" style={{ height: `${(point.expense / (maxTrendValue || 1)) * 100}%` }} />
                  </div>
                  <div className="trend-chart__legend">
                    <strong>{point.label}</strong>
                    <span>{formatCurrency(point.income)} / {formatCurrency(point.expense)}</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
      <div className="dashboard-grid">
        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Budget usage</h3>
            <p>Highest pressure budget categories this month.</p>
          </div>
          {summary.budgetUsage.length === 0 ? (
            <EmptyState title="No budgets configured" description="Create monthly budgets to compare plan versus actual spending." />
          ) : (
            <div className="simple-list">
              {summary.budgetUsage.map((item) => (
                <div key={item.budgetId} className="list-row list-row--stacked">
                  <div>
                    <strong>{item.categoryName}</strong>
                    <p>{formatCurrency(item.spent)} of {formatCurrency(item.budgeted)} used</p>
                  </div>
                  <div className="dashboard-inline-progress">
                    <ProgressBar value={item.usagePercent} tone={item.isOverBudget ? "danger" : item.isThresholdReached ? "warning" : "default"} />
                    <small>{item.usagePercent.toFixed(2)}% • {formatCurrency(item.remaining)} remaining</small>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Goal progress</h3>
            <p>Most relevant active and completed goals.</p>
          </div>
          {summary.goalProgress.length === 0 ? (
            <EmptyState title="No goals yet" description="Create savings goals to track progress against your targets." />
          ) : (
            <div className="simple-list">
              {summary.goalProgress.map((goal) => (
                <div key={goal.goalId} className={`list-row list-row--stacked goal-progress-row goal-progress-row--${goal.color ?? "teal"}`}>
                  <div className="goal-progress-row__header">
                    <div className="goal-card__title-group">
                      <span className={`goal-badge goal-badge--${goal.color ?? "teal"}`}>{goalBadges[goal.icon ?? "Shield"] ?? "S"}</span>
                      <div>
                        <strong>{goal.goalName}</strong>
                        <p>{goal.linkedAccountName ?? "Standalone goal"}</p>
                      </div>
                    </div>
                    <span className={`status-badge status-badge--${goal.status === "Completed" ? "warning" : "default"}`}>{goal.status}</span>
                  </div>
                  <ProgressBar value={goal.progressPercent} tone={goal.status === "Completed" ? "warning" : "default"} />
                  <div className="budget-card__metrics">
                    <span>{formatCurrency(goal.currentAmount)} saved</span>
                    <span>{formatCurrency(goal.targetAmount)} target</span>
                    <span>{goal.targetDateUtc ? formatDate(goal.targetDateUtc) : "Flexible date"}</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
      <div className="dashboard-grid">
        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Account balance split</h3>
            <p>Current balance distribution across active accounts.</p>
          </div>
          {summary.accountBalanceDistribution.length === 0 ? (
            <EmptyState title="No accounts available" description="Account balances will appear here once your ledgers are set up." />
          ) : (
            <div className="chart-list">
              {summary.accountBalanceDistribution.map((item) => (
                <div key={item.accountId} className="chart-row">
                  <div className="chart-row__label">
                    <span>{item.accountName}</span>
                    <strong>{formatCurrency(item.currentBalance, item.currencyCode)}</strong>
                  </div>
                  <div className="chart-bar chart-bar--muted">
                    <div className="chart-bar__fill chart-bar__fill--secondary" style={{ width: `${(item.currentBalance / (maxAccountBalance || 1)) * 100}%` }} />
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Recent transactions</h3>
            <p>Latest recorded activity across your ledger.</p>
          </div>
          {summary.recentTransactions.length === 0 ? (
            <EmptyState title="No transactions yet" description="Recent activity will appear here after you add your first transaction." />
          ) : (
            <div className="simple-list">
              {summary.recentTransactions.map((item) => (
                <div key={item.id} className="list-row">
                  <div>
                    <strong>{item.merchant || item.categoryName || item.type}</strong>
                    <p>{item.accountName} - {formatDate(item.dateUtc)}</p>
                  </div>
                  <strong>{formatCurrency(item.amount)}</strong>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
      <div className="dashboard-grid">
        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Budget health</h3>
            <p>Current month budget performance across planned expense categories.</p>
          </div>
          {summary.budgetHealth.totalBudgeted === 0 ? (
            <EmptyState title="No budgets configured" description="Create monthly budgets to track how actual expense activity compares with plan." />
          ) : (
            <div className="budget-health-card">
              <div className="budget-health-card__summary">
                <strong>{formatCurrency(summary.budgetHealth.totalSpent)} spent of {formatCurrency(summary.budgetHealth.totalBudgeted)}</strong>
                <span>{budgetUsage.toFixed(2)}% used</span>
              </div>
              <ProgressBar value={budgetUsage} tone={summary.budgetHealth.overBudgetCount > 0 ? "danger" : summary.budgetHealth.thresholdReachedCount > 0 ? "warning" : "default"} />
              <div className="budget-card__metrics">
                <span>{formatCurrency(summary.budgetHealth.totalRemaining)} remaining</span>
                <span>{summary.budgetHealth.thresholdReachedCount} threshold warnings</span>
                <span>{summary.budgetHealth.overBudgetCount} over budget</span>
              </div>
            </div>
          )}
        </section>
        <section className="panel-card">
          <div className="panel-card__header">
            <h3>Recent goal activity</h3>
            <p>Internal savings movements are shown separately from expenses and income.</p>
          </div>
          {summary.recentGoalActivities.length === 0 ? (
            <EmptyState title="No goal activity yet" description="Goal contributions and withdrawals will appear here once you start using goals." />
          ) : (
            <div className="simple-list">
              {summary.recentGoalActivities.map((item) => (
                <div key={item.id} className="list-row">
                  <div>
                    <strong>{item.type}: {item.goalName}</strong>
                    <p>{item.accountName ?? "No linked account"} - {formatDate(item.occurredAtUtc)}</p>
                    {item.note ? <small>{item.note}</small> : null}
                  </div>
                  <strong>{formatCurrency(item.amount)}</strong>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}