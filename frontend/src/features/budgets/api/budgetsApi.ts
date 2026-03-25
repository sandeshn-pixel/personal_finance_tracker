import { apiClient } from "../../../shared/lib/api/client";

export type BudgetDto = {
  id: string;
  categoryId: string;
  categoryName: string;
  categoryIsArchived: boolean;
  year: number;
  month: number;
  amount: number;
  alertThresholdPercent: number;
  actualSpent: number;
  remaining: number;
  percentageUsed: number;
  isOverBudget: boolean;
  isThresholdReached: boolean;
  canManage: boolean;
  ownerDisplayName: string;
};

export type BudgetSummaryDto = {
  year: number;
  month: number;
  totalBudgeted: number;
  totalSpent: number;
  totalRemaining: number;
  overBudgetCount: number;
  thresholdReachedCount: number;
};

export type CreateBudgetPayload = {
  categoryId: string;
  year: number;
  month: number;
  amount: number;
  alertThresholdPercent: number;
};

export type UpdateBudgetPayload = {
  amount: number;
  alertThresholdPercent: number;
};

export type CopyBudgetsPayload = {
  year: number;
  month: number;
  overwriteExisting?: boolean;
};

export const budgetsApi = {
  list: (accessToken: string, year: number, month: number) => apiClient<BudgetDto[]>(`/budgets?year=${year}&month=${month}`, { accessToken }),
  summary: (accessToken: string, year: number, month: number) => apiClient<BudgetSummaryDto>(`/budgets/summary?year=${year}&month=${month}`, { accessToken }),
  create: (accessToken: string, payload: CreateBudgetPayload) => apiClient<BudgetDto>("/budgets", { method: "POST", body: JSON.stringify(payload), accessToken }),
  update: (accessToken: string, budgetId: string, payload: UpdateBudgetPayload) => apiClient<BudgetDto>(`/budgets/${budgetId}`, { method: "PUT", body: JSON.stringify(payload), accessToken }),
  remove: (accessToken: string, budgetId: string) => apiClient<void>(`/budgets/${budgetId}`, { method: "DELETE", accessToken }),
  copyPreviousMonth: (accessToken: string, payload: CopyBudgetsPayload) => apiClient<BudgetDto[]>("/budgets/copy-previous-month", { method: "POST", body: JSON.stringify(payload), accessToken }),
};
