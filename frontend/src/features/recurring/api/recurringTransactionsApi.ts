import { apiClient } from "../../../shared/lib/api/client";

export type RecurringFrequency = "Daily" | "Weekly" | "Monthly" | "Yearly";
export type RecurringRuleStatus = "Active" | "Paused" | "Completed" | "Deleted";
export type TransactionType = "Income" | "Expense" | "Transfer";

export type RecurringTransactionDto = {
  id: string;
  title: string;
  type: TransactionType;
  amount: number;
  accountId: string;
  accountName: string;
  transferAccountId?: string | null;
  transferAccountName?: string | null;
  categoryId?: string | null;
  categoryName?: string | null;
  frequency: RecurringFrequency;
  startDateUtc: string;
  endDateUtc?: string | null;
  nextRunDateUtc?: string | null;
  autoCreateTransaction: boolean;
  status: RecurringRuleStatus;
  canManage: boolean;
  createdUtc: string;
  updatedUtc: string;
  lastProcessedAtUtc?: string | null;
};

export type RecurringTransactionPayload = {
  title: string;
  type: number;
  amount: number;
  categoryId?: string | null;
  accountId: string;
  transferAccountId?: string | null;
  frequency: number;
  startDateUtc: string;
  endDateUtc?: string | null;
  autoCreateTransaction: boolean;
};

export type RecurringExecutionSummaryDto = {
  rulesVisited: number;
  transactionsCreated: number;
  occurrencesProcessed: number;
  occurrencesSkipped: number;
  processedAtUtc: string;
};

export const recurringTransactionsApi = {
  list: (accessToken: string) => apiClient<RecurringTransactionDto[]>("/recurring-transactions", { accessToken }),
  create: (accessToken: string, payload: RecurringTransactionPayload) => apiClient<RecurringTransactionDto>("/recurring-transactions", { method: "POST", body: JSON.stringify(payload), accessToken }),
  update: (accessToken: string, ruleId: string, payload: RecurringTransactionPayload) => apiClient<RecurringTransactionDto>(`/recurring-transactions/${ruleId}`, { method: "PUT", body: JSON.stringify(payload), accessToken }),
  pause: (accessToken: string, ruleId: string) => apiClient<RecurringTransactionDto>(`/recurring-transactions/${ruleId}/pause`, { method: "POST", accessToken }),
  resume: (accessToken: string, ruleId: string) => apiClient<RecurringTransactionDto>(`/recurring-transactions/${ruleId}/resume`, { method: "POST", accessToken }),
  remove: (accessToken: string, ruleId: string) => apiClient<void>(`/recurring-transactions/${ruleId}`, { method: "DELETE", accessToken }),
  processDue: (accessToken: string) => apiClient<RecurringExecutionSummaryDto>("/recurring-transactions/process-due", { method: "POST", accessToken }),
};
