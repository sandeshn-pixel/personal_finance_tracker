import { apiClient } from "../../../shared/lib/api/client";
import type { TransactionDto } from "../../transactions/api/transactionsApi";

export type CategorySpendDto = {
  categoryId: string;
  categoryName: string;
  amount: number;
};

export type BudgetHealthDto = {
  totalBudgeted: number;
  totalSpent: number;
  totalRemaining: number;
  overBudgetCount: number;
  thresholdReachedCount: number;
};

export type SavingsAutomationSummaryDto = {
  totalContributedToGoals: number;
  totalWithdrawnFromGoals: number;
  netGoalSavings: number;
  activeGoalsCount: number;
  completedGoalsCount: number;
  activeRecurringRulesCount: number;
  pausedRecurringRulesCount: number;
  dueRecurringRulesCount: number;
};

export type RecentGoalActivityDto = {
  id: string;
  goalId: string;
  goalName: string;
  type: "Contribution" | "Withdrawal";
  amount: number;
  occurredAtUtc: string;
  note?: string | null;
  accountName?: string | null;
};

export type DashboardSummaryDto = {
  currentMonthIncome: number;
  currentMonthExpense: number;
  netBalance: number;
  recentTransactions: TransactionDto[];
  spendingByCategory: CategorySpendDto[];
  budgetHealth: BudgetHealthDto;
  savingsAutomation: SavingsAutomationSummaryDto;
  recentGoalActivities: RecentGoalActivityDto[];
};

export const dashboardApi = {
  summary: (accessToken: string) => apiClient<DashboardSummaryDto>("/dashboard/summary", { accessToken }),
};