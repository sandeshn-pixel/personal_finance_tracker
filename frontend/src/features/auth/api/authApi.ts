import { apiClient } from "../../../shared/lib/api/client";
import type { AuthResponse, LoginPayload, RegisterPayload } from "./types";

export const authApi = {
  login: (payload: LoginPayload) => apiClient<AuthResponse>("/auth/login", {
    method: "POST",
    body: JSON.stringify(payload),
  }),
  signup: (payload: RegisterPayload) => apiClient<AuthResponse>("/auth/register", {
    method: "POST",
    body: JSON.stringify(payload),
  }),
  refresh: () => apiClient<AuthResponse>("/auth/refresh", {
    method: "POST",
  }),
  logout: () => apiClient<void>("/auth/logout", {
    method: "POST",
  }),
};
