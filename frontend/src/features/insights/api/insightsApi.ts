import { apiClient } from "../../../shared/lib/api/client";

export type HealthScoreBand = "Poor" | "Fair" | "Good" | "Strong";
export type InsightLevel = "Info" | "Positive" | "Attention";
export type ReportTimeBucket = "Auto" | "Week" | "Month";

export type HealthScoreFactorDto = {
  key: string;
  title: string;
  score: number;
  weightPercent: number;
  weightedPoints: number;
  metricValue: number;
  metricLabel: string;
  explanation: string;
  isFallback: boolean;
};

export type HealthScoreResponseDto = {
  score: number;
  band: HealthScoreBand;
  hasSparseData: boolean;
  lookbackStartUtc: string;
  lookbackEndUtc: string;
  summary: string;
  factors: HealthScoreFactorDto[];
  suggestions: string[];
};

export type InsightItemDto = {
  key: string;
  title: string;
  message: string;
  basis: string;
  level: InsightLevel;
  isFallback: boolean;
};

export type InsightsResponseDto = {
  startDateUtc: string;
  endDateUtc: string;
  comparisonStartUtc: string;
  comparisonEndUtc: string;
  hasSparseData: boolean;
  summary: string;
  items: InsightItemDto[];
};

export type HealthScoreQuery = {
  accountId?: string;
  accountIds?: string[];
};

export type InsightsQuery = {
  startDateUtc: string;
  endDateUtc: string;
  bucket?: ReportTimeBucket;
  accountId?: string;
  accountIds?: string[];
  categoryId?: string;
  categoryIds?: string[];
};

function buildQuery(query?: HealthScoreQuery | InsightsQuery) {
  const params = new URLSearchParams();
  if (!query) {
    return "";
  }

  if ("startDateUtc" in query) {
    params.set("startDateUtc", query.startDateUtc);
    params.set("endDateUtc", query.endDateUtc);
    if (query.bucket && query.bucket !== "Auto") {
      params.set("bucket", query.bucket);
    }
    if (query.categoryId) {
      params.set("categoryId", query.categoryId);
    }
    if (query.categoryIds?.length) {
      for (const categoryId of query.categoryIds) {
        params.append("categoryIds", categoryId);
      }
    }
  }

  if (query.accountId) {
    params.set("accountId", query.accountId);
  }
  if (query.accountIds?.length) {
    for (const accountId of query.accountIds) {
      params.append("accountIds", accountId);
    }
  }

  const serialized = params.toString();
  return serialized ? `?${serialized}` : "";
}

export const insightsApi = {
  list: (accessToken: string, query: InsightsQuery) => apiClient<InsightsResponseDto>(`/insights${buildQuery(query)}`, { accessToken }),
  healthScore: (accessToken: string, query?: HealthScoreQuery) => apiClient<HealthScoreResponseDto>(`/insights/health-score${buildQuery(query)}`, { accessToken }),
};
