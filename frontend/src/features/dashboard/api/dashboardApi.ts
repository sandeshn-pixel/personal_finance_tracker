import { apiClient } from "../../../shared/lib/api/client";
import type { TransactionDto } from "../../transactions/api/transactionsApi";

export type CategorySpendDto = {
  categoryId: string;
  categoryName: string;
  amount: number;
};

export type TrendPointDto = {
  label: string;
  income: number;
  expense: number;
};

export type AccountBalanceSliceDto = {
  accountId: string;
  accountName: string;
  accountType: string;
  currencyCode: string;
  currentBalance: number;
};

export type GoalProgressDto = {
  goalId: string;
  goalName: string;
  icon?: string | null;
  color?: string | null;
  currentAmount: number;
  targetAmount: number;
  progressPercent: number;
  linkedAccountName?: string | null;
  targetDateUtc?: string | null;
  status: "Active" | "Completed" | "Archived";
};

export type BudgetUsageItemDto = {
  budgetId: string;
  categoryId: string;
  categoryName: string;
  budgeted: number;
  spent: number;
  remaining: number;
  usagePercent: number;
  isOverBudget: boolean;
  isThresholdReached: boolean;
  canManage: boolean;
  ownerDisplayName: string;
};

export type BudgetHealthDto = {
  totalBudgeted: number;
  totalSpent: number;
  totalRemaining: number;
  overBudgetCount: number;
  thresholdReachedCount: number;
  sharedReadOnlyBudgetCount: number;
  sharedOwnerCount: number;
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
  incomeExpenseTrend: TrendPointDto[];
  accountBalanceDistribution: AccountBalanceSliceDto[];
  goalProgress: GoalProgressDto[];
  budgetUsage: BudgetUsageItemDto[];
  budgetHealth: BudgetHealthDto;
  savingsAutomation: SavingsAutomationSummaryDto;
  recentGoalActivities: RecentGoalActivityDto[];
};

export type DashboardQuery = {
  accountId?: string;
  accountIds?: string[];
};

function buildDashboardQuery(query?: DashboardQuery) {
  const params = new URLSearchParams();
  if (query?.accountId) {
    params.set("accountId", query.accountId);
  }
  if (query?.accountIds?.length) {
    for (const accountId of query.accountIds) {
      params.append("accountIds", accountId);
    }
  }

  const serialized = params.toString();
  return serialized ? `?${serialized}` : "";
}

export const dashboardApi = {
  summary: (accessToken: string, query?: DashboardQuery) => apiClient<DashboardSummaryDto>(`/dashboard/summary${buildDashboardQuery(query)}`, { accessToken }),
};
