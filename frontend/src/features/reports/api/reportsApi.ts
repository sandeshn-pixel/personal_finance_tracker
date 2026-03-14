import { apiClient } from "../../../shared/lib/api/client";

export type ReportSummaryDto = {
  totalIncome: number;
  totalExpense: number;
  netCashFlow: number;
  expenseTransactionCount: number;
  incomeTransactionCount: number;
};

export type CategorySpendReportItemDto = {
  categoryId: string;
  categoryName: string;
  amount: number;
};

export type IncomeExpenseTrendPointDto = {
  periodStartUtc: string;
  label: string;
  income: number;
  expense: number;
};

export type AccountBalanceTrendPointDto = {
  periodStartUtc: string;
  label: string;
  balance: number;
};

export type ReportsOverviewDto = {
  summary: ReportSummaryDto;
  categorySpend: CategorySpendReportItemDto[];
  incomeExpenseTrend: IncomeExpenseTrendPointDto[];
  accountBalanceTrend: AccountBalanceTrendPointDto[];
};

export type ReportsQuery = {
  startDateUtc: string;
  endDateUtc: string;
  accountId?: string;
};

function toQueryString(query: ReportsQuery) {
  const params = new URLSearchParams();
  params.set("startDateUtc", query.startDateUtc);
  params.set("endDateUtc", query.endDateUtc);
  if (query.accountId) {
    params.set("accountId", query.accountId);
  }
  return params.toString();
}

export const reportsApi = {
  overview: (accessToken: string, query: ReportsQuery) => apiClient<ReportsOverviewDto>(`/reports/overview?${toQueryString(query)}`, { accessToken }),
};
