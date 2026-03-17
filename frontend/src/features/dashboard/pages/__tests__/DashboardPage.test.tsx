import { render, screen } from "@testing-library/react";
import { vi } from "vitest";

const useAuthMock = vi.fn();
const dashboardSummaryMock = vi.fn();
const accountsListMock = vi.fn();

vi.mock("../../../../app/providers/AuthProvider", () => ({
  useAuth: () => useAuthMock(),
}));
vi.mock("../../../accounts/api/accountsApi", () => ({
  accountsApi: { list: (...args: unknown[]) => accountsListMock(...args) },
}));
vi.mock("../../api/dashboardApi", () => ({
  dashboardApi: { summary: (...args: unknown[]) => dashboardSummaryMock(...args) },
}));

import { DashboardPage } from "../DashboardPage";

describe("DashboardPage", () => {
  it("renders key dashboard cards and activity panels", async () => {
    useAuthMock.mockReturnValue({ accessToken: "token" });
    accountsListMock.mockResolvedValue([{ id: "a1", isArchived: false }]);
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
      budgetHealth: { totalBudgeted: 500, totalSpent: 300, totalRemaining: 200, overBudgetCount: 0, thresholdReachedCount: 1 },
      savingsAutomation: { totalContributedToGoals: 100, totalWithdrawnFromGoals: 20, netGoalSavings: 80, activeGoalsCount: 2, completedGoalsCount: 1, activeRecurringRulesCount: 3, pausedRecurringRulesCount: 1, dueRecurringRulesCount: 1 },
      recentGoalActivities: [],
    });

    render(<DashboardPage />);

    expect(await screen.findByText("Current month income vs expense")).toBeInTheDocument();
    expect(screen.getByText("Saved to goals")).toBeInTheDocument();
    expect(screen.getByText("Recurring rules")).toBeInTheDocument();
    expect(screen.getByText("Account balance split")).toBeInTheDocument();
  });
});
