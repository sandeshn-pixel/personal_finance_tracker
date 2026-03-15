import { apiClient } from "../../../shared/lib/api/client";

export type GoalStatus = "Active" | "Completed" | "Archived";
export type GoalEntryType = "Contribution" | "Withdrawal";

export type GoalDto = {
  id: string;
  name: string;
  targetAmount: number;
  currentAmount: number;
  remainingAmount: number;
  progressPercent: number;
  targetDateUtc?: string | null;
  linkedAccountId?: string | null;
  linkedAccountName?: string | null;
  icon?: string | null;
  color?: string | null;
  status: GoalStatus;
  createdUtc: string;
  updatedUtc: string;
};

export type GoalEntryDto = {
  id: string;
  type: GoalEntryType;
  amount: number;
  goalAmountAfterEntry: number;
  occurredAtUtc: string;
  note?: string | null;
  accountId?: string | null;
  accountName?: string | null;
  createdUtc: string;
};

export type GoalDetailsDto = {
  goal: GoalDto;
  entries: GoalEntryDto[];
};

export type GoalPayload = {
  name: string;
  targetAmount: number;
  targetDateUtc?: string | null;
  linkedAccountId?: string | null;
  icon?: string;
  color?: string;
};

export type GoalEntryPayload = {
  amount: number;
  occurredAtUtc?: string | null;
  note?: string;
};

export const goalsApi = {
  list: (accessToken: string) => apiClient<GoalDto[]>("/goals", { accessToken }),
  get: (accessToken: string, goalId: string) => apiClient<GoalDetailsDto>(`/goals/${goalId}`, { accessToken }),
  create: (accessToken: string, payload: GoalPayload) => apiClient<GoalDto>("/goals", { method: "POST", body: JSON.stringify(payload), accessToken }),
  update: (accessToken: string, goalId: string, payload: GoalPayload) => apiClient<GoalDto>(`/goals/${goalId}`, { method: "PUT", body: JSON.stringify(payload), accessToken }),
  contribute: (accessToken: string, goalId: string, payload: GoalEntryPayload) => apiClient<GoalDetailsDto>(`/goals/${goalId}/contributions`, { method: "POST", body: JSON.stringify(payload), accessToken }),
  withdraw: (accessToken: string, goalId: string, payload: GoalEntryPayload) => apiClient<GoalDetailsDto>(`/goals/${goalId}/withdrawals`, { method: "POST", body: JSON.stringify(payload), accessToken }),
  complete: (accessToken: string, goalId: string) => apiClient<GoalDto>(`/goals/${goalId}/complete`, { method: "POST", accessToken }),
  archive: (accessToken: string, goalId: string) => apiClient<GoalDto>(`/goals/${goalId}/archive`, { method: "POST", accessToken }),
};