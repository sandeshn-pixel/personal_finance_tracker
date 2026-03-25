import { apiClient, downloadFile } from "../../../shared/lib/api/client";

export type ReportSummaryDto = {
  totalIncome: number;
  totalExpense: number;
  netCashFlow: number;
  expenseTransactionCount: number;
  incomeTransactionCount: number;
};

export type ReportPeriodComparisonDto = {
  previousTotalIncome: number;
  previousTotalExpense: number;
  previousNetCashFlow: number;
  previousExpenseTransactionCount: number;
  previousIncomeTransactionCount: number;
};

export type CategorySpendReportItemDto = {
  categoryId: string;
  categoryName: string;
  amount: number;
};

export type MerchantSpendReportItemDto = {
  merchantName: string;
  amount: number;
  transactionCount: number;
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
  comparison: ReportPeriodComparisonDto;
  categorySpend: CategorySpendReportItemDto[];
  topMerchants: MerchantSpendReportItemDto[];
  incomeExpenseTrend: IncomeExpenseTrendPointDto[];
  accountBalanceTrend: AccountBalanceTrendPointDto[];
};

export type ReportsQuery = {
  startDateUtc: string;
  endDateUtc: string;
  accountId?: string;
  accountIds?: string[];
};

export function toQueryString(query: ReportsQuery) {
  const params = new URLSearchParams();
  params.set("startDateUtc", query.startDateUtc);
  params.set("endDateUtc", query.endDateUtc);
  if (query.accountId) {
    params.set("accountId", query.accountId);
  }
  if (query.accountIds?.length) {
    for (const accountId of query.accountIds) {
      params.append("accountIds", accountId);
    }
  }
  return params.toString();
}

export const reportsApi = {
  overview: (accessToken: string, query: ReportsQuery) => apiClient<ReportsOverviewDto>(`/reports/overview?${toQueryString(query)}`, { accessToken }),
  exportOverviewCsv: (accessToken: string, query: ReportsQuery) => downloadFile(`/exports/reports/overview.csv?${toQueryString(query)}`, "reports-overview.csv", { accessToken }),
  exportOverviewPdf: (accessToken: string, query: ReportsQuery) => downloadFile(`/exports/reports/overview.pdf?${toQueryString(query)}`, "reports-overview.pdf", { accessToken }),
};
