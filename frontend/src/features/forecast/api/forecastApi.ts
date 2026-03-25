import { apiClient } from "../../../shared/lib/api/client";

export type ForecastRiskLevel = "Low" | "Medium" | "High";

export type ForecastRecurringItemDto = {
  scheduledDateUtc: string;
  title: string;
  type: "Income" | "Expense" | "Transfer";
  amount: number;
  accountName: string;
};

export type ForecastRecurringSummaryDto = {
  totalExpectedIncome: number;
  totalExpectedExpense: number;
  netExpectedImpact: number;
  itemCount: number;
  items: ForecastRecurringItemDto[];
};

export type ForecastMonthSummaryDto = {
  currentBalance: number;
  projectedEndOfMonthBalance: number;
  minimumProjectedBalance: number;
  safeToSpend: number;
  averageDailyIncome: number;
  averageDailyExpense: number;
  averageDailyNet: number;
  daysRemainingInMonth: number;
  hasSparseData: boolean;
  riskLevel: ForecastRiskLevel;
  basisDescription: string;
  upcomingRecurring: ForecastRecurringSummaryDto;
  notes: string[];
};

export type ForecastDayPointDto = {
  dateUtc: string;
  projectedBalance: number;
  baselineNetChange: number;
  recurringNetChange: number;
};

export type ForecastDailyResponseDto = {
  summary: ForecastMonthSummaryDto;
  points: ForecastDayPointDto[];
};

export type ForecastQuery = {
  accountId?: string;
  accountIds?: string[];
};

function buildQuery(query?: ForecastQuery) {
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

export const forecastApi = {
  month: (accessToken: string, query?: ForecastQuery) => apiClient<ForecastMonthSummaryDto>(`/forecast/month${buildQuery(query)}`, { accessToken }),
  daily: (accessToken: string, query?: ForecastQuery) => apiClient<ForecastDailyResponseDto>(`/forecast/daily${buildQuery(query)}`, { accessToken }),
};
