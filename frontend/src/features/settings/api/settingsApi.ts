import { apiClient } from "../../../shared/lib/api/client";
import type { ThemeName } from "../../../app/providers/ThemeProvider";

export type SettingsDateFormat = "dd MMM yyyy" | "dd/MM/yyyy" | "MM/dd/yyyy" | "yyyy-MM-dd";
export type SettingsLandingPage =
  | "/dashboard"
  | "/transactions"
  | "/accounts"
  | "/budgets"
  | "/goals"
  | "/reports"
  | "/recurring"
  | "/settings";

export type UserSettingsDto = {
  profile: {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
  };
  preferences: {
    preferredCurrencyCode: string;
    dateFormat: SettingsDateFormat;
    landingPage: SettingsLandingPage;
    theme: ThemeName;
  };
  notifications: {
    budgetWarningsEnabled: boolean;
    goalRemindersEnabled: boolean;
    recurringRemindersEnabled: boolean;
  };
  financialDefaults: {
    defaultAccountId: string | null;
    defaultAccountName: string | null;
    defaultPaymentMethod: string | null;
    defaultBudgetAlertThresholdPercent: number;
  };
};

export type SampleDataSeedStatusDto = {
  canSeedFromDashboard: boolean;
  canRunSeed: boolean;
  hasTransactions: boolean;
  activeAccountCount: number;
  budgetCount: number;
  goalCount: number;
  recurringRuleCount: number;
};

export type SeedSampleDataResultDto = {
  message: string;
  accountsCreated: number;
  transactionsCreated: number;
  budgetsCreated: number;
  goalsCreated: number;
  recurringRulesCreated: number;
};

export const settingsApi = {
  get: (accessToken: string) => apiClient<UserSettingsDto>("/settings", { accessToken }),
  updateProfile: (accessToken: string, payload: { firstName: string; lastName: string; email: string }) => apiClient<UserSettingsDto["profile"]>("/settings/profile", { method: "PUT", body: JSON.stringify(payload), accessToken }),
  changePassword: (accessToken: string, payload: { currentPassword: string; newPassword: string }) => apiClient<void>("/settings/change-password", { method: "POST", body: JSON.stringify(payload), accessToken }),
  updatePreferences: (accessToken: string, payload: { preferredCurrencyCode: string; dateFormat: SettingsDateFormat; landingPage: SettingsLandingPage; theme: ThemeName }) => apiClient<UserSettingsDto["preferences"]>("/settings/preferences", { method: "PUT", body: JSON.stringify(payload), accessToken }),
  updateNotifications: (accessToken: string, payload: { budgetWarningsEnabled: boolean; goalRemindersEnabled: boolean; recurringRemindersEnabled: boolean }) => apiClient<UserSettingsDto["notifications"]>("/settings/notifications", { method: "PUT", body: JSON.stringify(payload), accessToken }),
  updateFinancialDefaults: (accessToken: string, payload: { defaultAccountId: string | null; defaultPaymentMethod?: string | null; defaultBudgetAlertThresholdPercent: number }) => apiClient<UserSettingsDto["financialDefaults"]>("/settings/financial-defaults", { method: "PUT", body: JSON.stringify(payload), accessToken }),
  getSampleDataStatus: (accessToken: string) => apiClient<SampleDataSeedStatusDto>("/settings/sample-data-status", { accessToken }),
  seedSampleData: (accessToken: string) => apiClient<SeedSampleDataResultDto>("/settings/sample-data", { method: "POST", accessToken }),
  logoutAll: (accessToken: string) => apiClient<void>("/settings/logout-all", { method: "POST", accessToken }),
};
