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

function buildQuery(accountId?: string) {
  return accountId ? `?accountId=${encodeURIComponent(accountId)}` : "";
}

export const forecastApi = {
  month: (accessToken: string, accountId?: string) => apiClient<ForecastMonthSummaryDto>(`/forecast/month${buildQuery(accountId)}`, { accessToken }),
  daily: (accessToken: string, accountId?: string) => apiClient<ForecastDailyResponseDto>(`/forecast/daily${buildQuery(accountId)}`, { accessToken }),
};
