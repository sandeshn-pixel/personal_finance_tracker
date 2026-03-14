import { apiClient } from "../../../shared/lib/api/client";

export type AccountType = "BankAccount" | "CreditCard" | "CashWallet" | "SavingsAccount";

export type AccountDto = {
  id: string;
  name: string;
  type: AccountType;
  currencyCode: string;
  openingBalance: number;
  currentBalance: number;
  institutionName?: string | null;
  last4Digits?: string | null;
  isArchived: boolean;
};

export type AccountPayload = {
  name: string;
  type: number;
  currencyCode: string;
  openingBalance?: number;
  institutionName?: string;
  last4Digits?: string;
};

export const accountsApi = {
  list: (accessToken: string, includeArchived = false) => apiClient<AccountDto[]>(`/accounts?includeArchived=${includeArchived}`, { accessToken }),
  create: (accessToken: string, payload: AccountPayload) => apiClient<AccountDto>("/accounts", { method: "POST", body: JSON.stringify(payload), accessToken }),
  update: (accessToken: string, id: string, payload: Omit<AccountPayload, "openingBalance">) => apiClient<AccountDto>(`/accounts/${id}`, { method: "PUT", body: JSON.stringify(payload), accessToken }),
  archive: (accessToken: string, id: string) => apiClient<void>(`/accounts/${id}`, { method: "DELETE", accessToken }),
};
