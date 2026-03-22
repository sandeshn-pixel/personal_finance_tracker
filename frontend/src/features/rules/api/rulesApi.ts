import { apiClient } from "../../../shared/lib/api/client";

export type RuleConditionField = "Merchant" | "Amount" | "Category" | "TransactionType" | "Account";
export type RuleConditionOperator = "Equals" | "Contains" | "GreaterThan" | "LessThan";
export type RuleActionType = "SetCategory" | "AddTag" | "CreateAlert";
export type TransactionType = "Income" | "Expense" | "Transfer";

export type RuleConditionDto = {
  field: RuleConditionField;
  operator: RuleConditionOperator;
  textValue?: string | null;
  amountValue?: number | null;
  categoryId?: string | null;
  accountId?: string | null;
  transactionType?: TransactionType | null;
};

export type RuleActionDto = {
  type: RuleActionType;
  categoryId?: string | null;
  tag?: string | null;
  alertTitle?: string | null;
  alertMessage?: string | null;
};

export type TransactionRuleDto = {
  id: string;
  name: string;
  priority: number;
  isActive: boolean;
  condition: RuleConditionDto;
  action: RuleActionDto;
  conditionSummary: string;
  actionSummary: string;
  createdUtc: string;
  updatedUtc: string;
};

export type UpsertTransactionRuleRequest = {
  name: string;
  priority: number;
  isActive: boolean;
  condition: RuleConditionDto;
  action: RuleActionDto;
};

export const rulesApi = {
  list: (accessToken: string) => apiClient<TransactionRuleDto[]>("/rules", { accessToken }),
  create: (accessToken: string, payload: UpsertTransactionRuleRequest) =>
    apiClient<TransactionRuleDto>("/rules", {
      method: "POST",
      body: JSON.stringify(payload),
      accessToken,
    }),
  update: (accessToken: string, ruleId: string, payload: UpsertTransactionRuleRequest) =>
    apiClient<TransactionRuleDto>(`/rules/${ruleId}`, {
      method: "PUT",
      body: JSON.stringify(payload),
      accessToken,
    }),
  remove: (accessToken: string, ruleId: string) =>
    apiClient<void>(`/rules/${ruleId}`, {
      method: "DELETE",
      accessToken,
    }),
};

