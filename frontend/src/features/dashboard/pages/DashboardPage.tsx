import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../../app/providers/AuthProvider";
import { useWorkspaceScope } from "../../../app/providers/WorkspaceScopeProvider";
import { accountsApi, type AccountDto } from "../../accounts/api/accountsApi";
import { dashboardApi, type DashboardSummaryDto } from "../api/dashboardApi";
import { forecastApi, type ForecastDailyResponseDto, type ForecastMonthSummaryDto } from "../../forecast/api/forecastApi";
import { insightsApi, type HealthScoreResponseDto } from "../../insights/api/insightsApi";
import { Alert } from "../../../shared/components/Alert";
import { EmptyState } from "../../../shared/components/EmptyState";
import { PageLoader } from "../../../shared/components/PageLoader";
import { ProgressBar } from "../../../shared/components/ProgressBar";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { StatCard } from "../../../shared/components/StatCard";
import { ApiError } from "../../../shared/lib/api/client";
import { formatCurrency, formatDate } from "../../../shared/lib/format";
import { filterAccountsForView, getScopedAccountIdsForView, hasSharedGuestAccounts } from "../../../shared/lib/sharedAccessView";

const goalBadges: Record<string, string> = {
  Shield: "S",
  Home: "H",
  Plane: "T",
  Book: "E",
  Car: "C",
  Gift: "G",
};

const spendingPalette = ["#00ADB5", "#F08F86", "#6F7D8C", "#A86523", "#7E57C2", "#4CAF50"];
const forecastRiskTone: Record<string, "default" | "warning" | "danger"> = {
  Low: "default",
  Medium: "warning",
  High: "danger",
};

export function DashboardPage() {
  const { accessToken } = useAuth();
  const { sharedAccessView } = useWorkspaceScope();
  const [summary, setSummary] = useState<DashboardSummaryDto | null>(null);
  const [forecastMonth, setForecastMonth] = useState<ForecastMonthSummaryDto | null>(null);
  const [forecastDaily, setForecastDaily] = useState<ForecastDailyResponseDto | null>(null);
  const [healthScore, setHealthScore] = useState<HealthScoreResponseDto | null>(null);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [errorVariant, setErrorVariant] = useState<"error" | "info">("error");

  useEffect(() => {
    void load();
  }, [accessToken, sharedAccessView]);

  async function load() {
    if (!accessToken) return;
    setLoading(true);
    try {
      const accountResponse = await accountsApi.list(accessToken);
      const activeAccountResponse = accountResponse.filter((item) => !item.isArchived);
      const scopedAccountIds = getScopedAccountIdsForView(activeAccountResponse, sharedAccessView);
      setAccounts(accountResponse);

      if (sharedAccessView !== "all" && activeAccountResponse.length > 0 && scopedAccountIds.length === 0) {
        setSummary(null);
        setForecastMonth(null);
        setForecastDaily(null);
        setHealthScore(null);
        setErrorVariant("error");
        setErrorMessage(null);
        return;
      }

      const scopeQuery = scopedAccountIds.length > 0 ? { accountIds: scopedAccountIds } : undefined;

      const [summaryResponse, forecastMonthResponse, forecastDailyResponse, healthScoreResponse] = await Promise.all([
        dashboardApi.summary(accessToken, scopeQuery),
        forecastApi.month(accessToken, scopeQuery),
        forecastApi.daily(accessToken, scopeQuery),
        insightsApi.healthScore(accessToken, scopeQuery),
      ]);

      setSummary(summaryResponse);
      setForecastMonth(forecastMonthResponse);
      setForecastDaily(forecastDailyResponse);
      setHealthScore(healthScoreResponse);
      setErrorVariant("error");
      setErrorMessage(null);
    } catch (error) {
      if (error instanceof ApiError && error.status === 429) {
        setErrorVariant("info");
        setErrorMessage("Dashboard insights are refreshing too quickly. Please wait a moment and try again.");
      } else {
        setErrorVariant("error");
        setErrorMessage(error instanceof ApiError ? error.message : "Unable to load dashboard.");
      }
    } finally {
      setLoading(false);
    }
  }

  const activeAccounts = useMemo(() => accounts.filter((item) => !item.isArchived), [accounts]);
  const scopedActiveAccounts = useMemo(() => filterAccountsForView(activeAccounts, sharedAccessView), [activeAccounts, sharedAccessView]);
  const showSharedViewToggle = useMemo(() => hasSharedGuestAccounts(activeAccounts), [activeAccounts]);
  const sharedGuestAccounts = useMemo(() => scopedActiveAccounts.filter((item) => item.isShared && item.currentUserRole !== "Owner"), [scopedActiveAccounts]);
  const isScopeEmpty = sharedAccessView !== "all" && activeAccounts.length > 0 && scopedActiveAccounts.length === 0;
  const accountCount = scopedActiveAccounts.length;
  const accountLookup = useMemo(() => new Map(scopedActiveAccounts.map((item) => [item.id, item])), [scopedActiveAccounts]);
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

  const forecastPoints = forecastDaily?.points ?? [];
  const forecastMaxBalance = useMemo(() => Math.max(...forecastPoints.map((point) => point.projectedBalance), forecastMonth?.currentBalance ?? 0), [forecastPoints, forecastMonth]);
  const forecastMinBalance = useMemo(() => Math.min(...forecastPoints.map((point) => point.projectedBalance), forecastMonth?.currentBalance ?? 0), [forecastPoints, forecastMonth]);
  const forecastRange = Math.max(forecastMaxBalance - forecastMinBalance, 1);
  const topRecurringItems = useMemo(() => forecastMonth?.upcomingRecurring.items.slice(0, 4) ?? [], [forecastMonth]);
  const projectedDelta = forecastMonth ? forecastMonth.projectedEndOfMonthBalance - forecastMonth.currentBalance : 0;
  const topHealthFactors = useMemo(
    () => [...(healthScore?.factors ?? [])].sort((left, right) => left.score - right.score).slice(0, 2),
    [healthScore]
  );

  const scopeMessage = useMemo(() => {
    if (sharedAccessView === "shared" && sharedGuestAccounts.length > 0) {
      return `Dashboard is currently focused on ${sharedGuestAccounts.length} shared account${sharedGuestAccounts.length === 1 ? "" : "s"}. Forecasts and health score use only that shared activity, while goals and recurring automation remain owner-managed in this phase.`;
    }

    if (sharedAccessView === "mine" && showSharedViewToggle) {
      return "Dashboard is currently focused on your own accounts only. Shared-account balances, forecasts, and health metrics are excluded from this view.";
    }

    if (sharedGuestAccounts.length > 0) {
      return `You currently have access to ${sharedGuestAccounts.length} shared account${sharedGuestAccounts.length === 1 ? "" : "s"}. Balances, recent transactions, reports, forecasts, and health score include those shared accounts, while budgets, goals, and recurring automation remain owner-managed in this phase.`;
    }

    return null;
  }, [sharedAccessView, sharedGuestAccounts, showSharedViewToggle]);
  if (loading) return <PageLoader label="Loading dashboard" />;

  if (isScopeEmpty) {
    return (
      <div className="page-stack dashboard-page">
        <SectionHeader title="Dashboard" description="A quick operating view of cashflow, balances, budgets, goals, recurring activity, and recent ledger movement." />
        {errorMessage ? <Alert message={errorMessage} variant={errorVariant} /> : null}
        {scopeMessage ? <Alert message={scopeMessage} variant="info" /> : null}
        <section className="dashboard-section">
          <section className="panel-card">
            <EmptyState
              title="No accounts in this view"
              description={sharedAccessView === "mine" ? "Mine is focused on accounts you own. There are no owned accounts available in the current workspace." : "Shared with me is focused on guest access. There are no shared guest accounts available in the current workspace."}
            />
            <div className="section-header__actions">
              <Link to="/accounts" className="ghost-button">Go to accounts</Link>
            </div>
          </section>
        </section>
      </div>
    );
  }

  if (!summary || !forecastMonth || !forecastDaily || !healthScore) return <Alert message={errorMessage ?? "Dashboard is unavailable."} variant={errorVariant} />;

  const isFirstRun = accountCount === 0
    && summary.recentTransactions.length === 0
    && summary.budgetHealth.totalBudgeted === 0
    && summary.goalProgress.length === 0
    && summary.savingsAutomation.activeRecurringRulesCount === 0;

  return (
    <div className="page-stack dashboard-page">
      <SectionHeader title="Dashboard" description="A quick operating view of cashflow, balances, budgets, goals, recurring activity, and recent ledger movement." />
      {errorMessage ? <Alert message={errorMessage} variant={errorVariant} /> : null}
      {scopeMessage ? <Alert message={scopeMessage} variant="info" /> : null}

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
                  <EmptyState title="No cashflow yet" description="Add income or expense activity to see the current month split." action={<Link to="/transactions" className="ghost-button">Add transaction</Link>} />
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
                  label="Projected month-end balance"
                  value={formatCurrency(forecastMonth.projectedEndOfMonthBalance)}
                  hint={`${forecastMonth.daysRemainingInMonth} days left | ${forecastMonth.riskLevel} risk.`}
                  tone={forecastMonth.projectedEndOfMonthBalance < 0 ? "negative" : projectedDelta >= 0 ? "positive" : undefined}
                />
                <StatCard
                  label="Safe to spend this month"
                  value={formatCurrency(forecastMonth.safeToSpend)}
                  hint={forecastMonth.hasSparseData ? "Estimate based mostly on known recurring items because recent history is limited." : "Estimated amount you can still spend this month without likely going below zero."}
                  tone={forecastMonth.safeToSpend <= 0 ? "negative" : "positive"}
                />
                <StatCard
                  label="Budget remaining"
                  value={formatCurrency(summary.budgetHealth.totalRemaining)}
                  hint={`${summary.budgetHealth.overBudgetCount} over budget, ${summary.budgetHealth.thresholdReachedCount} warnings.${summary.budgetHealth.sharedReadOnlyBudgetCount > 0 ? ` ${summary.budgetHealth.sharedReadOnlyBudgetCount} shared read-only budget ${summary.budgetHealth.sharedReadOnlyBudgetCount === 1 ? "summary" : "summaries"} included.` : ""}` }
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
              <section className="panel-card panel-card--large dashboard-flow__item forecast-card">
                <div className="panel-card__header">
                  <h3>Balance forecast</h3>
                  <p>Estimated day-by-day balance from today to month end using recent activity and known recurring items.</p>
                </div>
                <div className="forecast-summary-strip">
                  <div>
                    <span>Current</span>
                    <strong>{formatCurrency(forecastMonth.currentBalance)}</strong>
                  </div>
                  <div>
                    <span>Month-end</span>
                    <strong>{formatCurrency(forecastMonth.projectedEndOfMonthBalance)}</strong>
                  </div>
                  <div>
                    <span>Lowest point this month</span>
                    <strong>{formatCurrency(forecastMonth.minimumProjectedBalance)}</strong>
                  </div>
                  <span className={`status-badge status-badge--${forecastRiskTone[forecastMonth.riskLevel]}`}>{forecastMonth.riskLevel} risk</span>
                </div>
                {forecastPoints.length === 0 ? (
                  <EmptyState title="No forecast yet" description="Forecast projections appear once at least one active account is available." action={<Link to="/accounts" className="ghost-button">Add account</Link>} />
                ) : (
                  <>
                    <div className="forecast-chart" aria-label="Projected daily balance through month end">
                      {forecastPoints.map((point) => {
                        const height = ((point.projectedBalance - forecastMinBalance) / forecastRange) * 100;
                        const tone = point.projectedBalance < 0 ? "forecast-chart__bar--negative" : point.recurringNetChange < 0 ? "forecast-chart__bar--expense" : "forecast-chart__bar--default";

                        return (
                          <div key={point.dateUtc} className="forecast-chart__column" title={`${formatDate(point.dateUtc)}: ${formatCurrency(point.projectedBalance)}`}>
                            <div className="forecast-chart__track">
                              <div className={`forecast-chart__bar ${tone}`} style={{ height: `${Math.max(height, 8)}%` }} />
                            </div>
                            <small>{new Date(point.dateUtc).getDate()}</small>
                          </div>
                        );
                      })}
                    </div>
                    <div className="forecast-card__footer">
                      <p>{forecastMonth.basisDescription}</p>
                      <small>Estimate only. This shows where your balance may go during the rest of the month.</small>
                    </div>
                  </>
                )}
                {forecastMonth.notes.length > 0 ? (
                  <div className="forecast-note-list">
                    {forecastMonth.notes.map((note) => (
                      <div key={note} className="forecast-note-list__item">{note}</div>
                    ))}
                  </div>
                ) : null}
              </section>

              <section className="panel-card panel-card--compact dashboard-flow__item forecast-card">
                <div className="panel-card__header">
                  <h3>Upcoming recurring impact</h3>
                  <p>Known scheduled income and expenses still expected before month end.</p>
                </div>
                {forecastMonth.upcomingRecurring.itemCount === 0 ? (
                  <EmptyState title="No upcoming recurring items" description="Active recurring rules due later this month will appear here." action={<Link to="/recurring" className="ghost-button">Create recurring rule</Link>} />
                ) : (
                  <>
                    <div className="health-tile-grid forecast-impact-grid">
                      <div className="health-tile">
                        <strong>{formatCurrency(forecastMonth.upcomingRecurring.totalExpectedIncome)}</strong>
                        <span>Expected income</span>
                      </div>
                      <div className="health-tile">
                        <strong>{formatCurrency(forecastMonth.upcomingRecurring.totalExpectedExpense)}</strong>
                        <span>Expected expense</span>
                      </div>
                      <div className="health-tile">
                        <strong>{formatCurrency(forecastMonth.upcomingRecurring.netExpectedImpact)}</strong>
                        <span>Net impact</span>
                      </div>
                    </div>
                    <div className="simple-list">
                      {topRecurringItems.map((item) => (
                        <div key={`${item.title}-${item.scheduledDateUtc}-${item.accountName}`} className="list-row list-row--stacked">
                          <div className="forecast-recurring-row">
                            <div>
                              <strong>{item.title}</strong>
                              <p>{item.accountName} - {formatDate(item.scheduledDateUtc)}</p>
                            </div>
                            <strong className={item.type === "Expense" ? "text-danger" : "text-success"}>{formatCurrency(item.amount)}</strong>
                          </div>
                        </div>
                      ))}
                    </div>
                  </>
                )}
              </section>



              <section className="panel-card panel-card--compact dashboard-flow__item insights-dashboard-card">
                <div className="panel-card__header">
                  <h3>Financial health score</h3>
                  <p>An explainable 0 to 100 score based on savings rate, spending stability, budget adherence, and cash buffer.</p>
                </div>
                <div className="insights-dashboard-card__hero">
                  <div>
                    <span>Overall score</span>
                    <strong>{healthScore.score}/100</strong>
                    <p>{healthScore.summary}</p>
                  </div>
                  <span className={`status-badge status-badge--${healthScore.band === "Poor" ? "danger" : healthScore.band === "Fair" ? "warning" : "default"}`}>
                    {healthScore.band}
                  </span>
                </div>
                {healthScore.hasSparseData ? (
                  <div className="insights-dashboard-card__summary">
                    Recent history is limited, so one or more factors are using neutral fallback logic.
                  </div>
                ) : null}
                <div className="insights-dashboard-card__factors">
                  {topHealthFactors.map((factor) => (
                    <div key={factor.key} className="insights-dashboard-card__factor">
                      <div className="insights-dashboard-card__factor-head">
                        <strong>{factor.title}</strong>
                        <span>{factor.score}/100</span>
                      </div>
                      <ProgressBar value={factor.score} tone={factor.score < 40 ? "danger" : factor.score < 60 ? "warning" : "default"} />
                    </div>
                  ))}
                </div>
                <Link to="/insights/health-score" className="insights-dashboard-card__link">
                  View full score breakdown
                </Link>
              </section>

              <section className="panel-card panel-card--large dashboard-flow__item">
                <div className="panel-card__header">
                  <h3>Spending by category</h3>
                  <p>Current month expense distribution with the highest-impact categories first.</p>
                </div>
                {summary.spendingByCategory.length === 0 ? (
                  <EmptyState title="No expense activity" description="The chart appears once expense transactions are recorded." action={<Link to="/transactions" className="ghost-button">Add expense</Link>} />
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
                  <EmptyState title="No transactions yet" description="Recent activity will appear here after you add your first transaction." action={<Link to="/transactions" className="ghost-button">Add transaction</Link>} />
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
                            {accountLookup.get(item.accountId)?.isShared ? (
                              <span className="status-badge status-badge--default">Shared {accountLookup.get(item.accountId)?.currentUserRole}</span>
                            ) : null}
                            <span className={`transaction-type-pill transaction-type-pill--${item.type.toLowerCase()}`}>{item.categoryName || item.type}</span>
                            <span>{formatDate(item.dateUtc)}</span>
                            <span>By {item.updatedByDisplayName}</span>
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
                  <EmptyState title="No budgets configured" description="Create monthly budgets to track how actual expense activity compares with plan." action={<Link to="/budgets" className="ghost-button">Create budget</Link>} />
                ) : (
                  <div className="budget-health-card">
                    {summary.budgetHealth.sharedReadOnlyBudgetCount > 0 ? (
                      <div className="forecast-note-list">
                        <div className="forecast-note-list__item">
                          Includes {summary.budgetHealth.sharedReadOnlyBudgetCount} read-only shared budget {summary.budgetHealth.sharedReadOnlyBudgetCount === 1 ? "summary" : "summaries"} from {summary.budgetHealth.sharedOwnerCount} other account owner{summary.budgetHealth.sharedOwnerCount === 1 ? "" : "s"}.
                        </div>
                      </div>
                    ) : null}
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
                  <EmptyState title="No budgets configured" description="Create monthly budgets to compare plan versus actual spending." action={<Link to="/budgets" className="ghost-button">Create budget</Link>} />
                ) : (
                  <div className="simple-list">
                    {topBudgetUsage.map((item) => (
                      <div key={item.budgetId} className="list-row list-row--stacked">
                        <div>
                          <strong>{item.categoryName}</strong>
                          <p>{formatCurrency(item.spent)} of {formatCurrency(item.budgeted)} used</p>
                          {!item.canManage ? <small>Shared budget from {item.ownerDisplayName}</small> : null}
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
                  <EmptyState title="No accounts available" description="Account balances will appear here once your ledgers are set up." action={<Link to="/accounts" className="ghost-button">Add account</Link>} />
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
                            <div>
                              <span>{item.accountName}</span>
                              {accountLookup.get(item.accountId)?.isShared ? (
                                <small>Shared | {accountLookup.get(item.accountId)?.currentUserRole} | Owner {accountLookup.get(item.accountId)?.ownerDisplayName}</small>
                              ) : null}
                            </div>
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
                  <EmptyState title="No goals yet" description="Create savings goals to track progress against your targets." action={<Link to="/goals" className="ghost-button">Create goal</Link>} />
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

















