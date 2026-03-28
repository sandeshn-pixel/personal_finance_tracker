import { render, screen } from "@testing-library/react";
import { vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { WorkspaceScopeProvider } from "../../../../app/providers/WorkspaceScopeProvider";

const useAuthMock = vi.fn();
const dashboardSummaryMock = vi.fn();
const accountsListMock = vi.fn();
const forecastMonthMock = vi.fn();
const forecastDailyMock = vi.fn();
const healthScoreMock = vi.fn();
const reportsTrendsMock = vi.fn();
const sampleDataStatusMock = vi.fn();

vi.mock("../../../../app/providers/AuthProvider", () => ({
  useAuth: () => useAuthMock(),
}));
vi.mock("../../../accounts/api/accountsApi", () => ({
  accountsApi: { list: (...args: unknown[]) => accountsListMock(...args) },
}));
vi.mock("../../api/dashboardApi", () => ({
  dashboardApi: { summary: (...args: unknown[]) => dashboardSummaryMock(...args) },
}));
vi.mock("../../../forecast/api/forecastApi", () => ({
  forecastApi: {
    month: (...args: unknown[]) => forecastMonthMock(...args),
    daily: (...args: unknown[]) => forecastDailyMock(...args),
  },
}));
vi.mock("../../../insights/api/insightsApi", () => ({
  insightsApi: { healthScore: (...args: unknown[]) => healthScoreMock(...args) },
}));
vi.mock("../../../reports/api/reportsApi", () => ({
  reportsApi: { trends: (...args: unknown[]) => reportsTrendsMock(...args) },
}));
vi.mock("../../../settings/api/settingsApi", () => ({
  settingsApi: {
    getSampleDataStatus: (...args: unknown[]) => sampleDataStatusMock(...args),
    seedSampleData: vi.fn(),
  },
}));

import { DashboardPage } from "../DashboardPage";

describe("DashboardPage", () => {
  it("renders key dashboard cards and activity panels", async () => {
    useAuthMock.mockReturnValue({ accessToken: "token" });
    accountsListMock.mockResolvedValue([{ id: "a1", name: "Primary", isArchived: false, isShared: false, currentUserRole: "Owner", ownerDisplayName: "Test User" }]);
    dashboardSummaryMock.mockResolvedValue({
      currentMonthIncome: 1200,
      currentMonthExpense: 300,
      netBalance: 900,
      recentTransactions: [],
      spendingByCategory: [],
      incomeExpenseTrend: [],
      accountBalanceDistribution: [
        { accountId: "a1", accountName: "Primary", accountType: "BankAccount", currencyCode: "INR", currentBalance: 900 },
      ],
      goalProgress: [],
      budgetUsage: [],
      budgetHealth: { totalBudgeted: 500, totalSpent: 300, totalRemaining: 200, overBudgetCount: 0, thresholdReachedCount: 1, sharedReadOnlyBudgetCount: 0, sharedOwnerCount: 0 },
      savingsAutomation: { totalContributedToGoals: 100, totalWithdrawnFromGoals: 20, netGoalSavings: 80, activeGoalsCount: 2, completedGoalsCount: 1, activeRecurringRulesCount: 3, pausedRecurringRulesCount: 1, dueRecurringRulesCount: 1 },
      recentGoalActivities: [],
    });
    forecastMonthMock.mockResolvedValue({
      currentBalance: 900,
      projectedEndOfMonthBalance: 1100,
      minimumProjectedBalance: 850,
      safeToSpend: 850,
      averageDailyIncome: 100,
      averageDailyExpense: 40,
      averageDailyNet: 60,
      daysRemaining: 10,
      hasSparseData: false,
      riskLevel: "Low",
      basisDescription: "Test basis",
      upcomingRecurring: {
        totalExpectedIncome: 100,
        totalExpectedExpense: 50,
        netExpectedImpact: 50,
        itemCount: 1,
        items: [{ scheduledDateUtc: "2026-03-20T00:00:00Z", title: "Salary", type: "Income", amount: 100, accountName: "Primary" }],
      },
      notes: [],
    });
    forecastDailyMock.mockResolvedValue({
      summary: {
        currentBalance: 900,
        projectedEndOfMonthBalance: 1100,
        minimumProjectedBalance: 850,
        safeToSpend: 850,
        averageDailyIncome: 100,
        averageDailyExpense: 40,
        averageDailyNet: 60,
        daysRemaining: 10,
        hasSparseData: false,
        riskLevel: "Low",
        basisDescription: "Test basis",
        upcomingRecurring: {
          totalExpectedIncome: 100,
          totalExpectedExpense: 50,
          netExpectedImpact: 50,
          itemCount: 1,
          items: [{ scheduledDateUtc: "2026-03-20T00:00:00Z", title: "Salary", type: "Income", amount: 100, accountName: "Primary" }],
        },
        notes: [],
      },
      points: [{ dateUtc: "2026-03-18T00:00:00Z", projectedBalance: 950, averageDailyNet: 60, recurringNetChange: 0 }],
    });
    healthScoreMock.mockResolvedValue({
      score: 82,
      band: "Strong",
      hasSparseData: false,
      lookbackStartUtc: "2025-12-01T00:00:00Z",
      lookbackEndUtc: "2026-02-28T00:00:00Z",
      summary: "Healthy",
      factors: [
        { key: "savings-rate", title: "Savings rate", score: 80, weightPercent: 30, weightedPoints: 24, metricValue: 20, metricLabel: "Savings rate %", explanation: "", isFallback: false },
        { key: "cash-buffer", title: "Cash buffer", score: 75, weightPercent: 30, weightedPoints: 22.5, metricValue: 2, metricLabel: "Months of expenses covered", explanation: "", isFallback: false },
      ],
      suggestions: [],
    });
    reportsTrendsMock.mockResolvedValue({
      startDateUtc: "2025-10-01T00:00:00Z",
      endDateUtc: "2026-03-01T00:00:00Z",
      bucket: "Month",
      hasSparseData: false,
      basisDescription: "Test basis",
      incomeExpenseTrend: [{ periodStartUtc: "2026-03-01T00:00:00Z", label: "Mar 2026", income: 1200, expense: 300 }],
      savingsRateTrend: [],
      categoryTrends: [],
    });
    sampleDataStatusMock.mockResolvedValue({
      canSeedFromDashboard: false,
      canRunSeed: false,
      hasTransactions: true,
      activeAccountCount: 1,
      budgetCount: 1,
      goalCount: 1,
      recurringRuleCount: 1,
    });

    render(
      <MemoryRouter>
        <WorkspaceScopeProvider>
          <DashboardPage />
        </WorkspaceScopeProvider>
      </MemoryRouter>,
    );

    expect(await screen.findByText("Current month income vs expense")).toBeInTheDocument();
    expect(screen.getByText("Saved to goals")).toBeInTheDocument();
    expect(screen.getByText("Recurring rules")).toBeInTheDocument();
    expect(screen.getByText("Financial health score")).toBeInTheDocument();
  });
});
