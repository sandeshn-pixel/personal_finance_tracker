import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { vi } from "vitest";

const useAuthMock = vi.fn();
const categoriesListMock = vi.fn();
const categoriesCreateMock = vi.fn();

vi.mock("../../../../app/providers/AuthProvider", () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock("../../api/categoriesApi", () => ({
  categoriesApi: {
    list: (...args: unknown[]) => categoriesListMock(...args),
    create: (...args: unknown[]) => categoriesCreateMock(...args),
    update: vi.fn(),
    archive: vi.fn(),
  },
}));

import { CategoriesPage } from "../CategoriesPage";

describe("CategoriesPage", () => {
  it("loads categories and creates a new one", async () => {
    useAuthMock.mockReturnValue({ accessToken: "token" });
    categoriesListMock
      .mockResolvedValueOnce([
        { id: "c1", name: "Food", type: "Expense", isSystem: true, isArchived: false },
        { id: "c2", name: "Salary", type: "Income", isSystem: true, isArchived: false },
      ])
      .mockResolvedValueOnce([
        { id: "c1", name: "Food", type: "Expense", isSystem: true, isArchived: false },
        { id: "c2", name: "Salary", type: "Income", isSystem: true, isArchived: false },
        { id: "c3", name: "Dining out", type: "Expense", isSystem: false, isArchived: false },
      ]);
    categoriesCreateMock.mockResolvedValue({ id: "c3", name: "Dining out", type: "Expense", isSystem: false, isArchived: false });

    render(
      <MemoryRouter>
        <CategoriesPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/category list/i)).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText(/category name/i), "Dining out");
    await userEvent.click(screen.getByRole("button", { name: /create category/i }));

    await waitFor(() => expect(categoriesCreateMock).toHaveBeenCalledWith("token", { name: "Dining out", type: 2 }));
    expect(await screen.findByText("Dining out")).toBeInTheDocument();
  });
});
