import { apiClient } from "../../../shared/lib/api/client";
import type { AuthResponse, LoginPayload, RegisterPayload } from "./types";

export type ForgotPasswordResponse = {
  message: string;
  resetUrl: string | null;
  debugStatus: string | null;
};

export const authApi = {
  login: (payload: LoginPayload) => apiClient<AuthResponse>("/auth/login", {
    method: "POST",
    body: JSON.stringify(payload),
  }),
  signup: (payload: RegisterPayload) => apiClient<AuthResponse>("/auth/register", {
    method: "POST",
    body: JSON.stringify(payload),
  }),
  forgotPassword: (payload: { email: string }) => apiClient<ForgotPasswordResponse>("/auth/forgot-password", {
    method: "POST",
    body: JSON.stringify(payload),
  }),
  resetPassword: (payload: { email: string; token: string; newPassword: string }) => apiClient<void>("/auth/reset-password", {
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
