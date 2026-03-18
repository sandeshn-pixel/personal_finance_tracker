import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
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

const spendingPalette = ["#00ADB5", "#F08F86", "#6F7D8C", "#A86523", "#7E57C2", "#4CAF50"];

export function DashboardPage() {
  const { accessToken } = useAuth();
  const [summary, setSummary] = useState<DashboardSummaryDto | null>(null);
  const [accountCount, setAccountCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    void load();
  }, [accessToken]);

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
  const maxAccountBalance = useMemo(() => Math.max(...(summary?.accountBalanceDistribution.map((item) => item.currentBalance) ?? [0])), [summary]);
  const totalAccountBalance = useMemo(
    () => summary?.accountBalanceDistribution.reduce((total, item) => total + item.currentBalance, 0) ?? 0,
    [summary]
  );
  const currentMonthIncome = summary?.currentMonthIncome ?? 0;
  const currentMonthExpense = summary?.currentMonthExpense ?? 0;
  const budgetUsage = summary?.budgetHealth.totalBudgeted
    ? (summary.budgetHealth.totalSpent / summary.budgetHealth.totalBudgeted) * 100
    : 0;
  const currentMonthCashflowTotal = currentMonthIncome + currentMonthExpense;
  const incomeShare = currentMonthCashflowTotal > 0 ? (currentMonthIncome / currentMonthCashflowTotal) * 100 : 0;
  const expenseShare = currentMonthCashflowTotal > 0 ? (currentMonthExpense / currentMonthCashflowTotal) * 100 : 0;
  const topSpendingCategories = useMemo(() => summary?.spendingByCategory.slice(0, 5) ?? [], [summary]);
  const remainingSpendingAmount = useMemo(() => {
    if (!summary || summary.spendingByCategory.length <= 5) return 0;
    return summary.spendingByCategory.slice(5).reduce((total, item) => total + item.amount, 0);
  }, [summary]);
  const topBudgetUsage = useMemo(() => summary?.budgetUsage.slice(0, 4) ?? [], [summary]);
  const topGoalProgress = useMemo(() => summary?.goalProgress.slice(0, 3) ?? [], [summary]);
  const incomeExpenseChartStyle = {
    background: `conic-gradient(var(--color-success) 0deg ${(incomeShare / 100) * 360}deg, var(--color-danger) ${(incomeShare / 100) * 360}deg 360deg)`,
  };

  if (loading) return <PageLoader label="Loading dashboard" />;
  if (!summary) return <Alert message={errorMessage ?? "Dashboard is unavailable."} />;

  const isFirstRun = accountCount === 0
    && summary.recentTransactions.length === 0
    && summary.budgetHealth.totalBudgeted === 0
    && summary.goalProgress.length === 0
    && summary.savingsAutomation.activeRecurringRulesCount === 0;

  return (
    <div className="page-stack dashboard-page">
      <SectionHeader title="Dashboard" description="A quick operating view of cashflow, balances, budgets, goals, recurring activity, and recent ledger movement." />
      {errorMessage ? <Alert message={errorMessage} /> : null}

      {isFirstRun ? (
        <section className="dashboard-section">
          <section className="panel-card dashboard-onboarding-card">
            <div className="dashboard-onboarding-card__hero">
              <div>
                <p className="eyebrow">Getting started</p>
                <h3>Start tracking your money</h3>
                <p>Create your first account, record a transaction, or set a budget to turn this empty workspace into a live financial dashboard.</p>
              </div>
              <div className="dashboard-onboarding-card__actions">
                <Link to="/accounts" className="primary-button dashboard-onboarding-card__primary-link">Add your first account</Link>
                <div className="dashboard-onboarding-card__secondary-actions">
                  <Link to="/transactions" className="ghost-button">Add a transaction</Link>
                  <Link to="/budgets" className="ghost-button">Create a budget</Link>
                  <Link to="/goals" className="ghost-button">Create a goal</Link>
                </div>
              </div>
            </div>
            <div className="dashboard-onboarding-grid">
              <article className="dashboard-onboarding-step">
                <span className="dashboard-onboarding-step__index">1</span>
                <div>
                  <strong>Add an account</strong>
                  <p>Set up a bank account, wallet, or savings ledger so balances and transfers have a safe place to live.</p>
                </div>
              </article>
              <article className="dashboard-onboarding-step">
                <span className="dashboard-onboarding-step__index">2</span>
                <div>
                  <strong>Record your first transaction</strong>
                  <p>Add income, expenses, or transfers to start building cashflow history and recent activity.</p>
                </div>
              </article>
              <article className="dashboard-onboarding-step">
                <span className="dashboard-onboarding-step__index">3</span>
                <div>
                  <strong>Set a budget or goal</strong>
                  <p>Track spending limits and savings targets once your first account and transactions are in place.</p>
                </div>
              </article>
            </div>
          </section>
        </section>
      ) : (
        <>
      <section className="dashboard-section dashboard-section--hero">
        <div className="dashboard-hero-grid">
          <section className="panel-card panel-card--hero dashboard-hero-card">
            <div className="panel-card__header">
              <h3>Current month income vs expense</h3>
              <p>A single-view cashflow split for this month.</p>
            </div>
            {currentMonthCashflowTotal === 0 ? (
              <EmptyState title="No cashflow yet" description="Add income or expense activity to see the current month split." />
            ) : (
              <div className="donut-snapshot">
                <div className="donut-snapshot__chart" style={incomeExpenseChartStyle}>
                  <div className="donut-snapshot__center">
                    <strong>{formatCurrency(currentMonthCashflowTotal)}</strong>
                    <span>Total flow</span>
                  </div>
                </div>
                <div className="donut-snapshot__legend">
                  <div className="donut-snapshot__legend-row">
                    <span className="donut-swatch donut-swatch--income" />
                    <div>
                      <strong>Income</strong>
                      <p>{formatCurrency(currentMonthIncome)}</p>
                    </div>
                    <span>{incomeShare.toFixed(1)}%</span>
                  </div>
                  <div className="donut-snapshot__legend-row">
                    <span className="donut-swatch donut-swatch--expense" />
                    <div>
                      <strong>Expense</strong>
                      <p>{formatCurrency(currentMonthExpense)}</p>
                    </div>
                    <span>{expenseShare.toFixed(1)}%</span>
                  </div>
                  <small>Use Reports for longer month-by-month trend analysis.</small>
                </div>
              </div>
            )}
          </section>

          <div className="dashboard-summary-grid">
            <StatCard label="Net balance" value={formatCurrency(summary.netBalance)} hint={`${accountCount} active accounts included.`} />
            <StatCard
              label="Budget remaining"
              value={formatCurrency(summary.budgetHealth.totalRemaining)}
              hint={`${summary.budgetHealth.overBudgetCount} over budget, ${summary.budgetHealth.thresholdReachedCount} warnings.`}
              tone={summary.budgetHealth.totalRemaining < 0 ? "negative" : "positive"}
            />
            <StatCard
              label="Saved to goals"
              value={formatCurrency(summary.savingsAutomation.totalContributedToGoals)}
              hint={`${summary.savingsAutomation.activeGoalsCount} active goals.`}
              tone="positive"
            />
            <StatCard
              label="Recurring rules"
              value={String(summary.savingsAutomation.activeRecurringRulesCount)}
              hint={`${summary.savingsAutomation.dueRecurringRulesCount} due, ${summary.savingsAutomation.pausedRecurringRulesCount} paused.`}
            />
          </div>
        </div>
      </section>

      <section className="dashboard-section">
        <div className="dashboard-flow">
          <section className="panel-card panel-card--large dashboard-flow__item">
            <div className="panel-card__header">
              <h3>Spending by category</h3>
              <p>Current month expense distribution with the highest-impact categories first.</p>
            </div>
            {summary.spendingByCategory.length === 0 ? (
              <EmptyState title="No expense activity" description="The chart appears once expense transactions are recorded." />
            ) : (
              <div className="spending-bars">
                {topSpendingCategories.map((item, index) => {
                  const share = currentMonthExpense > 0 ? (item.amount / currentMonthExpense) * 100 : 0;
                  const color = spendingPalette[index % spendingPalette.length];

                  return (
                    <div key={item.categoryId} className="spending-bars__row">
                      <div className="spending-bars__label-group">
                        <span className="spending-bars__swatch" style={{ background: color }} />
                        <strong>{item.categoryName}</strong>
                      </div>
                      <div className="spending-bars__bar-wrap">
                        <div className="spending-bars__bar-track">
                          <div className="spending-bars__bar-fill" style={{ width: `${(item.amount / (maxSpend || 1)) * 100}%`, background: color }} />
                        </div>
                      </div>
                      <div className="spending-bars__value-group">
                        <strong>{formatCurrency(item.amount)}</strong>
                        <small>{share.toFixed(1)}%</small>
                      </div>
                    </div>
                  );
                })}
                {remainingSpendingAmount > 0 ? (
                  <div className="spending-bars__row spending-bars__row--other">
                    <div className="spending-bars__label-group">
                      <span className="spending-bars__swatch spending-bars__swatch--other" />
                      <strong>Other</strong>
                    </div>
                    <div className="spending-bars__bar-wrap">
                      <div className="spending-bars__bar-track">
                        <div className="spending-bars__bar-fill spending-bars__bar-fill--other" style={{ width: `${(remainingSpendingAmount / (maxSpend || 1)) * 100}%` }} />
                      </div>
                    </div>
                    <div className="spending-bars__value-group">
                      <strong>{formatCurrency(remainingSpendingAmount)}</strong>
                      <small>{((remainingSpendingAmount / currentMonthExpense) * 100).toFixed(1)}%</small>
                    </div>
                  </div>
                ) : null}
              </div>
            )}
          </section>

          <section className="panel-card panel-card--large dashboard-flow__item">
            <div className="panel-card__header">
              <h3>Recent transactions</h3>
              <p>Latest five recorded transactions across your ledger.</p>
            </div>
            {summary.recentTransactions.length === 0 ? (
              <EmptyState title="No transactions yet" description="Recent activity will appear here after you add your first transaction." />
            ) : (
              <div className="simple-list">
                {summary.recentTransactions.slice(0, 5).map((item) => (
                  <div key={item.id} className="list-row list-row--transaction-activity">
                    <div className={`activity-dot activity-dot--${item.type.toLowerCase()}`} />
                    <div className="transaction-activity__body">
                      <div className="transaction-activity__topline">
                        <strong className="transaction-activity__title">{item.merchant || item.categoryName || item.type}</strong>
                        <strong className="transaction-activity__amount-value">{formatCurrency(item.amount)}</strong>
                      </div>
                      <div className="transaction-activity__meta">
                        <span>{item.accountName}</span>
                        <span className={`transaction-type-pill transaction-type-pill--${item.type.toLowerCase()}`}>{item.categoryName || item.type}</span>
                        <span>{formatDate(item.dateUtc)}</span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>
            <section className="panel-card panel-card--compact dashboard-flow__item">
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
                <div className="health-tile-grid">
                  <div className="health-tile">
                    <strong>{formatCurrency(summary.budgetHealth.totalRemaining)}</strong>
                    <span>Remaining</span>
                  </div>
                  <div className="health-tile">
                    <strong>{summary.budgetHealth.thresholdReachedCount}</strong>
                    <span>Warnings</span>
                  </div>
                  <div className="health-tile">
                    <strong>{summary.budgetHealth.overBudgetCount}</strong>
                    <span>Over budget</span>
                  </div>
                </div>
              </div>
            )}
          </section>
          <section className="panel-card panel-card--medium dashboard-flow__item">
            <div className="panel-card__header">
              <h3>Budget usage</h3>
              <p>Highest pressure budget categories this month.</p>
            </div>
            {summary.budgetUsage.length === 0 ? (
              <EmptyState title="No budgets configured" description="Create monthly budgets to compare plan versus actual spending." />
            ) : (
              <div className="simple-list">
                {topBudgetUsage.map((item) => (
                  <div key={item.budgetId} className="list-row list-row--stacked">
                    <div>
                      <strong>{item.categoryName}</strong>
                      <p>{formatCurrency(item.spent)} of {formatCurrency(item.budgeted)} used</p>
                    </div>
                    <div className="dashboard-inline-progress">
                      <ProgressBar value={item.usagePercent} tone={item.isOverBudget ? "danger" : item.isThresholdReached ? "warning" : "default"} />
                      <small>{item.usagePercent.toFixed(2)}% | {formatCurrency(item.remaining)} remaining</small>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>

          <section className="panel-card panel-card--medium dashboard-flow__item">
            <div className="panel-card__header">
              <h3>Account balance split</h3>
              <p>Current balance distribution across active accounts.</p>
            </div>
            {summary.accountBalanceDistribution.length === 0 ? (
              <EmptyState title="No accounts available" description="Account balances will appear here once your ledgers are set up." />
            ) : (
              <div className="balance-split">
                <div className="balance-split__track">
                  {summary.accountBalanceDistribution.map((item) => (
                    <span
                      key={item.accountId}
                      className="balance-split__segment"
                      style={{ width: `${Math.max((item.currentBalance / (totalAccountBalance || 1)) * 100, 6)}%` }}
                      title={`${item.accountName}: ${formatCurrency(item.currentBalance, item.currencyCode)}`}
                    />
                  ))}
                </div>
                <div className="chart-list">
                  {summary.accountBalanceDistribution.map((item) => (
                    <div key={item.accountId} className="chart-row">
                      <div className="chart-row__label">
                        <span>{item.accountName}</span>
                        <div className="chart-row__value">
                          <strong>{formatCurrency(item.currentBalance, item.currencyCode)}</strong>
                          <small>{((item.currentBalance / (totalAccountBalance || 1)) * 100).toFixed(1)}%</small>
                        </div>
                      </div>
                      <div className="chart-bar chart-bar--muted">
                        <div className="chart-bar__fill chart-bar__fill--secondary" style={{ width: `${(item.currentBalance / (maxAccountBalance || 1)) * 100}%` }} />
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </section>

          <section className="panel-card panel-card--compact dashboard-flow__item">
            <div className="panel-card__header">
              <h3>Goal progress</h3>
              <p>Active and completed goals.</p>
            </div>
            {summary.goalProgress.length === 0 ? (
              <EmptyState title="No goals yet" description="Create savings goals to track progress against your targets." />
            ) : (
              <div className="simple-list">
                {topGoalProgress.map((goal) => (
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
      </section>
        </>
      )}
    </div>
  );
}
