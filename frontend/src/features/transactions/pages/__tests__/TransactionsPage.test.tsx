import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import { WorkspaceScopeProvider } from "../../../../app/providers/WorkspaceScopeProvider";

const useAuthMock = vi.fn();
const accountsListMock = vi.fn();
const categoriesListMock = vi.fn();
const transactionsListMock = vi.fn();
const transactionsExportMock = vi.fn();

vi.mock("../../../../app/providers/AuthProvider", () => ({
  useAuth: () => useAuthMock(),
}));
vi.mock("../../../accounts/api/accountsApi", () => ({
  accountsApi: { list: (...args: unknown[]) => accountsListMock(...args) },
}));
vi.mock("../../../categories/api/categoriesApi", () => ({
  categoriesApi: { list: (...args: unknown[]) => categoriesListMock(...args) },
}));
vi.mock("../../api/transactionsApi", () => ({
  transactionsApi: {
    list: (...args: unknown[]) => transactionsListMock(...args),
    exportCsv: (...args: unknown[]) => transactionsExportMock(...args),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
  },
}));

import { TransactionsPage } from "../TransactionsPage";

describe("TransactionsPage", () => {
  it("applies filters and exports with the current query", async () => {
    useAuthMock.mockReturnValue({ accessToken: "token" });
    accountsListMock.mockResolvedValue([{ id: "a1", name: "Checking", isArchived: false, isShared: false, currentUserRole: "Owner" }]);
    categoriesListMock.mockResolvedValue([{ id: "c1", name: "Food", type: "Expense", isArchived: false }]);
    transactionsListMock.mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0 });
    transactionsExportMock.mockResolvedValue("transactions.csv");

    render(
      <WorkspaceScopeProvider>
        <TransactionsPage />
      </WorkspaceScopeProvider>,
    );

    await waitFor(() => expect(transactionsListMock).toHaveBeenCalled());

    await userEvent.type(screen.getByPlaceholderText(/search merchant or note/i), "coffee");

    await waitFor(() => expect(transactionsListMock).toHaveBeenLastCalledWith("token", expect.objectContaining({ search: "coffee", page: 1, pageSize: 10 })));

    await userEvent.click(screen.getByRole("button", { name: /export filtered transactions to csv/i }));

    await waitFor(() => expect(transactionsExportMock).toHaveBeenCalledWith("token", expect.objectContaining({ search: "coffee" })));
  }, 10000);
});
