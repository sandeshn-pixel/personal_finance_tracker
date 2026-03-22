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

export const insightsApi = {
  healthScore: (accessToken: string) => apiClient<HealthScoreResponseDto>("/insights/health-score", { accessToken }),
};
