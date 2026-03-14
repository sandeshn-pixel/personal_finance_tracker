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

export type DashboardSummaryDto = {
  currentMonthIncome: number;
  currentMonthExpense: number;
  netBalance: number;
  recentTransactions: TransactionDto[];
  spendingByCategory: CategorySpendDto[];
  budgetHealth: BudgetHealthDto;
};

export const dashboardApi = {
  summary: (accessToken: string) => apiClient<DashboardSummaryDto>("/dashboard/summary", { accessToken }),
};
