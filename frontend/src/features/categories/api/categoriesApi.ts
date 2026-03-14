import { apiClient } from "../../../shared/lib/api/client";

export type CategoryDto = {
  id: string;
  name: string;
  type: "Income" | "Expense";
  isSystem: boolean;
  isArchived: boolean;
};

export const categoriesApi = {
  list: (accessToken: string, includeArchived = false) => apiClient<CategoryDto[]>(`/categories?includeArchived=${includeArchived}`, { accessToken }),
};
