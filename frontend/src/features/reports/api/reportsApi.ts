import { apiClient, downloadFile } from "../../../shared/lib/api/client";

export type ReportTimeBucket = "Auto" | "Week" | "Month";

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

export type SavingsRateTrendPointDto = {
  periodStartUtc: string;
  label: string;
  income: number;
  expense: number;
  netSavings: number;
  savingsRatePercent: number | null;
  hasIncomeData: boolean;
};

export type CategoryTrendPointDto = {
  periodStartUtc: string;
  label: string;
  amount: number;
};

export type CategoryTrendSeriesDto = {
  categoryId: string;
  categoryName: string;
  totalAmount: number;
  points: CategoryTrendPointDto[];
};

export type AccountBalanceTrendPointDto = {
  periodStartUtc: string;
  label: string;
  balance: number;
};

export type NetWorthTrendPointDto = {
  periodStartUtc: string;
  label: string;
  netWorth: number;
  assetBalance: number;
  liabilityBalance: number;
};

export type ReportsOverviewDto = {
  summary: ReportSummaryDto;
  comparison: ReportPeriodComparisonDto;
  categorySpend: CategorySpendReportItemDto[];
  topMerchants: MerchantSpendReportItemDto[];
  incomeExpenseTrend: IncomeExpenseTrendPointDto[];
  accountBalanceTrend: AccountBalanceTrendPointDto[];
};

export type ReportsTrendResponseDto = {
  startDateUtc: string;
  endDateUtc: string;
  bucket: ReportTimeBucket;
  hasSparseData: boolean;
  basisDescription: string;
  incomeExpenseTrend: IncomeExpenseTrendPointDto[];
  savingsRateTrend: SavingsRateTrendPointDto[];
  categoryTrends: CategoryTrendSeriesDto[];
};

export type NetWorthReportDto = {
  startDateUtc: string;
  endDateUtc: string;
  bucket: ReportTimeBucket;
  currentNetWorth: number;
  startingNetWorth: number;
  changeAmount: number;
  includedAccountCount: number;
  includedLiabilityAccountCount: number;
  basisDescription: string;
  points: NetWorthTrendPointDto[];
};

export type ReportsQuery = {
  startDateUtc: string;
  endDateUtc: string;
  bucket?: ReportTimeBucket;
  accountId?: string;
  accountIds?: string[];
  categoryId?: string;
  categoryIds?: string[];
};

export function toQueryString(query: ReportsQuery) {
  const params = new URLSearchParams();
  params.set("startDateUtc", query.startDateUtc);
  params.set("endDateUtc", query.endDateUtc);
  if (query.bucket && query.bucket !== "Auto") {
    params.set("bucket", query.bucket);
  }
  if (query.accountId) {
    params.set("accountId", query.accountId);
  }
  if (query.accountIds?.length) {
    for (const accountId of query.accountIds) {
      params.append("accountIds", accountId);
    }
  }
  if (query.categoryId) {
    params.set("categoryId", query.categoryId);
  }
  if (query.categoryIds?.length) {
    for (const categoryId of query.categoryIds) {
      params.append("categoryIds", categoryId);
    }
  }
  return params.toString();
}

export const reportsApi = {
  overview: (accessToken: string, query: ReportsQuery) => apiClient<ReportsOverviewDto>(`/reports/overview?${toQueryString(query)}`, { accessToken }),
  trends: (accessToken: string, query: ReportsQuery) => apiClient<ReportsTrendResponseDto>(`/reports/trends?${toQueryString(query)}`, { accessToken }),
  netWorth: (accessToken: string, query: ReportsQuery) => apiClient<NetWorthReportDto>(`/reports/net-worth?${toQueryString(query)}`, { accessToken }),
  exportOverviewCsv: (accessToken: string, query: ReportsQuery) => downloadFile(`/exports/reports/overview.csv?${toQueryString(query)}`, "reports-overview.csv", { accessToken }),
  exportOverviewPdf: (accessToken: string, query: ReportsQuery) => downloadFile(`/exports/reports/overview.pdf?${toQueryString(query)}`, "reports-overview.pdf", { accessToken }),
};
