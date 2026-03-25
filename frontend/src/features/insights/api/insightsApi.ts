import { apiClient } from "../../../shared/lib/api/client";

export type HealthScoreBand = "Poor" | "Fair" | "Good" | "Strong";

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

export type HealthScoreQuery = {
  accountId?: string;
  accountIds?: string[];
};

function buildQuery(query?: HealthScoreQuery) {
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

export const insightsApi = {
  healthScore: (accessToken: string, query?: HealthScoreQuery) => apiClient<HealthScoreResponseDto>(`/insights/health-score${buildQuery(query)}`, { accessToken }),
};
