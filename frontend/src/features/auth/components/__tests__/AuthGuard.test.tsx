import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { vi } from "vitest";

const useAuthMock = vi.fn();

vi.mock("../../../../app/providers/AuthProvider", () => ({
  useAuth: () => useAuthMock(),
}));

import { AuthGuard } from "../AuthGuard";

describe("AuthGuard", () => {
  it("redirects anonymous users to login", () => {
    useAuthMock.mockReturnValue({ status: "anonymous" });

    render(
      <MemoryRouter initialEntries={["/dashboard"]}>
        <Routes>
          <Route path="/login" element={<div>Login page</div>} />
          <Route element={<AuthGuard />}>
            <Route path="/dashboard" element={<div>Secret page</div>} />
          </Route>
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByText("Login page")).toBeInTheDocument();
  });

  it("renders children for authenticated users", () => {
    useAuthMock.mockReturnValue({ status: "authenticated" });

    render(
      <MemoryRouter initialEntries={["/dashboard"]}>
        <Routes>
          <Route element={<AuthGuard />}>
            <Route path="/dashboard" element={<div>Secret page</div>} />
          </Route>
        </Routes>
      </MemoryRouter>,
    );

    expect(screen.getByText("Secret page")).toBeInTheDocument();
  });
});
