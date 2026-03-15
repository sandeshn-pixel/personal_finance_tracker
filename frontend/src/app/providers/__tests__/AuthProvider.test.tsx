import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";

const refreshMock = vi.fn();
const logoutMock = vi.fn();

vi.mock("../../../features/auth/api/authApi", () => ({
  authApi: {
    refresh: (...args: unknown[]) => refreshMock(...args),
    logout: (...args: unknown[]) => logoutMock(...args),
    login: vi.fn(),
    signup: vi.fn(),
  },
}));

import { AuthProvider, useAuth } from "../AuthProvider";

function Consumer() {
  const { status, user, logout } = useAuth();
  return (
    <div>
      <span>{status}</span>
      <span>{user?.email ?? "none"}</span>
      <button type="button" onClick={() => void logout()}>Logout</button>
    </div>
  );
}

describe("AuthProvider", () => {
  it("restores the session on mount and clears it on logout", async () => {
    refreshMock.mockResolvedValue({
      accessToken: "token",
      expiresInSeconds: 900,
      user: { id: "1", email: "user@example.com", firstName: "Test", lastName: "User" },
    });
    logoutMock.mockResolvedValue(undefined);

    render(
      <AuthProvider>
        <Consumer />
      </AuthProvider>,
    );

    await waitFor(() => expect(screen.getByText("authenticated")).toBeInTheDocument());
    expect(screen.getByText("user@example.com")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /logout/i }));

    await waitFor(() => expect(screen.getByText("anonymous")).toBeInTheDocument());
    expect(screen.getByText("none")).toBeInTheDocument();
  });
});
