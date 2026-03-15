import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";

const useAuthMock = vi.fn();
const accountsListMock = vi.fn();
const reportsOverviewMock = vi.fn();
const reportsExportMock = vi.fn();

vi.mock("../../../../app/providers/AuthProvider", () => ({
  useAuth: () => useAuthMock(),
}));
vi.mock("../../../accounts/api/accountsApi", () => ({
  accountsApi: { list: (...args: unknown[]) => accountsListMock(...args) },
}));
vi.mock("../../api/reportsApi", () => ({
  reportsApi: {
    overview: (...args: unknown[]) => reportsOverviewMock(...args),
    exportOverviewCsv: (...args: unknown[]) => reportsExportMock(...args),
  },
}));

import { ReportsPage } from "../ReportsPage";

describe("ReportsPage", () => {
  it("renders report totals and exports the current report", async () => {
    useAuthMock.mockReturnValue({ accessToken: "token" });
    accountsListMock.mockResolvedValue([{ id: "a1", name: "Checking", isArchived: false }]);
    reportsOverviewMock.mockResolvedValue({
      summary: { totalIncome: 1000, totalExpense: 250, netCashFlow: 750, incomeTransactionCount: 1, expenseTransactionCount: 1 },
      categorySpend: [{ categoryId: "c1", categoryName: "Food", amount: 250 }],
      incomeExpenseTrend: [{ periodStartUtc: "2026-03-01T00:00:00Z", label: "Week of 01 Mar", income: 1000, expense: 250 }],
      accountBalanceTrend: [{ periodStartUtc: "2026-03-01T00:00:00Z", label: "Week of 01 Mar", balance: 750 }],
    });
    reportsExportMock.mockResolvedValue("reports-overview.csv");

    render(<ReportsPage />);

    expect(await screen.findByText("Total income")).toBeInTheDocument();
    expect(screen.getByText("Coverage")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /export current report overview to csv/i }));

    await waitFor(() => expect(reportsExportMock).toHaveBeenCalledWith("token", expect.objectContaining({ startDateUtc: expect.any(String), endDateUtc: expect.any(String) })));
  });
});
