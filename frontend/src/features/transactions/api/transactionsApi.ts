import { apiClient, downloadFile } from "../../../shared/lib/api/client";

export type TransactionDto = {
  id: string;
  accountId: string;
  accountName: string;
  transferAccountId?: string | null;
  transferAccountName?: string | null;
  type: "Income" | "Expense" | "Transfer";
  amount: number;
  dateUtc: string;
  categoryId?: string | null;
  categoryName?: string | null;
  note?: string | null;
  merchant?: string | null;
  paymentMethod?: string | null;
  recurringTransactionId?: string | null;
  tags: string[];
  createdUtc: string;
  updatedUtc: string;
};

export type TransactionListResponse = {
  items: TransactionDto[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type TransactionPayload = {
  accountId: string;
  transferAccountId?: string | null;
  type: number;
  amount: number;
  dateUtc: string;
  categoryId?: string | null;
  note?: string;
  merchant?: string;
  paymentMethod?: string;
  recurringTransactionId?: string | null;
  tags: string[];
};

export type TransactionQuery = {
  page?: number;
  pageSize?: number;
  startDateUtc?: string;
  endDateUtc?: string;
  categoryId?: string;
  accountId?: string;
  type?: string;
  minAmount?: string;
  maxAmount?: string;
  search?: string;
};

export function toSearchParams(query: TransactionQuery) {
  const params = new URLSearchParams();

  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === "") {
      continue;
    }

    params.set(key, String(value));
  }

  return params.toString();
}

export const transactionsApi = {
  list: (accessToken: string, query: TransactionQuery) => apiClient<TransactionListResponse>(`/transactions?${toSearchParams(query)}`, { accessToken }),
  create: (accessToken: string, payload: TransactionPayload) => apiClient<TransactionDto>("/transactions", { method: "POST", body: JSON.stringify(payload), accessToken }),
  update: (accessToken: string, id: string, payload: TransactionPayload) => apiClient<TransactionDto>(`/transactions/${id}`, { method: "PUT", body: JSON.stringify(payload), accessToken }),
  remove: (accessToken: string, id: string) => apiClient<void>(`/transactions/${id}`, { method: "DELETE", accessToken }),
  exportCsv: (accessToken: string, query: TransactionQuery) => downloadFile(`/exports/transactions.csv?${toSearchParams(query)}`, "transactions.csv", { accessToken }),
};
