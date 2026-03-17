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
  create: (accessToken: string, payload: { name: string; type: number }) =>
    apiClient<CategoryDto>("/categories", {
      method: "POST",
      body: JSON.stringify(payload),
      accessToken,
    }),
  update: (accessToken: string, categoryId: string, payload: { name: string }) =>
    apiClient<CategoryDto>(`/categories/${categoryId}`, {
      method: "PUT",
      body: JSON.stringify(payload),
      accessToken,
    }),
  archive: (accessToken: string, categoryId: string) =>
    apiClient<void>(`/categories/${categoryId}`, {
      method: "DELETE",
      accessToken,
    }),
};
