import { apiClient } from "../../../shared/lib/api/client";

export type AutomationRunSummaryDto = {
  usersProcessed: number;
  transactionsCreated: number;
  autoOccurrencesProcessed: number;
  manualRemindersCreated: number;
  goalRemindersCreated: number;
  processedAtUtc: string;
};

export type AutomationStatusDto = {
  backgroundProcessingEnabled: boolean;
  pollingIntervalSeconds: number;
  lastStartedUtc?: string | null;
  lastCompletedUtc?: string | null;
  lastRunSucceeded?: boolean | null;
  lastError?: string | null;
  lastSummary?: AutomationRunSummaryDto | null;
};

export const automationApi = {
  status: (accessToken: string) => apiClient<AutomationStatusDto>("/automation/status", { accessToken }),
};
