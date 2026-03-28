import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { vi } from "vitest";

const useAuthMock = vi.fn();
const useThemeMock = vi.fn();
const accountsListMock = vi.fn();
const settingsGetMock = vi.fn();
const settingsUpdatePreferencesMock = vi.fn();
const settingsSampleDataStatusMock = vi.fn();

vi.mock("../../../../app/providers/AuthProvider", () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock("../../../../app/providers/ThemeProvider", () => ({
  useTheme: () => useThemeMock(),
}));

vi.mock("../../../accounts/api/accountsApi", () => ({
  accountsApi: {
    list: (...args: unknown[]) => accountsListMock(...args),
  },
}));

vi.mock("../../api/settingsApi", () => ({
  settingsApi: {
    get: (...args: unknown[]) => settingsGetMock(...args),
    getSampleDataStatus: (...args: unknown[]) => settingsSampleDataStatusMock(...args),
    seedSampleData: vi.fn(),
    updatePreferences: (...args: unknown[]) => settingsUpdatePreferencesMock(...args),
    updateProfile: vi.fn(),
    changePassword: vi.fn(),
    updateNotifications: vi.fn(),
    updateFinancialDefaults: vi.fn(),
    logoutAll: vi.fn(),
  },
}));

import { SettingsPage } from "../SettingsPage";

describe("SettingsPage", () => {
  it("loads settings and applies the selected theme preference on save", async () => {
    const setThemeMock = vi.fn();
    useAuthMock.mockReturnValue({
      accessToken: "token",
      refreshSession: vi.fn(),
      logout: vi.fn(),
    });
    useThemeMock.mockReturnValue({
      theme: "slate",
      setTheme: setThemeMock,
    });
    accountsListMock.mockResolvedValue([]);
    settingsGetMock.mockResolvedValue({
      profile: { id: "user-1", email: "user@example.com", firstName: "Test", lastName: "User" },
      preferences: { preferredCurrencyCode: "INR", dateFormat: "dd MMM yyyy", landingPage: "/dashboard", theme: "slate" },
      notifications: { budgetWarningsEnabled: true, goalRemindersEnabled: true, recurringRemindersEnabled: true },
      financialDefaults: { defaultAccountId: null, defaultAccountName: null, defaultPaymentMethod: null, defaultBudgetAlertThresholdPercent: 80 },
    });
    settingsSampleDataStatusMock.mockResolvedValue({
      canSeedFromDashboard: true,
      canRunSeed: true,
      hasTransactions: false,
      activeAccountCount: 0,
      budgetCount: 0,
      goalCount: 0,
      recurringRuleCount: 0,
    });
    settingsUpdatePreferencesMock.mockResolvedValue({
      preferredCurrencyCode: "INR",
      dateFormat: "dd MMM yyyy",
      landingPage: "/dashboard",
      theme: "dark",
    });

    render(
      <MemoryRouter>
        <SettingsPage />
      </MemoryRouter>,
    );

    await waitFor(() => expect(settingsGetMock).toHaveBeenCalled());
    expect(setThemeMock).toHaveBeenCalledWith("slate");

    await userEvent.click(screen.getByRole("button", { name: /preferences/i }));
    await userEvent.click(screen.getByRole("button", { name: /dark mode/i }));
    await userEvent.click(screen.getByRole("button", { name: /save preferences/i }));

    await waitFor(() => expect(settingsUpdatePreferencesMock).toHaveBeenCalledWith("token", {
      preferredCurrencyCode: "INR",
      dateFormat: "dd MMM yyyy",
      landingPage: "/dashboard",
      theme: "dark",
    }));
    expect(setThemeMock).toHaveBeenLastCalledWith("dark");
    expect(await screen.findByText(/preferences updated/i)).toBeInTheDocument();
  }, 10000);
});
