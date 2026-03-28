import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { vi } from "vitest";

const forgotPasswordMock = vi.fn();

vi.mock("../../api/authApi", () => ({
  authApi: {
    forgotPassword: (...args: unknown[]) => forgotPasswordMock(...args),
  },
}));

import { ForgotPasswordPage } from "../ForgotPasswordPage";

describe("ForgotPasswordPage", () => {
  it("submits the email and shows the development reset link when returned", async () => {
    forgotPasswordMock.mockResolvedValue({
      message: "If the email exists, a password reset email has been prepared.",
      resetUrl: "http://localhost:5173/reset-password?email=user%40example.com&token=abc",
    });

    render(
      <MemoryRouter>
        <ForgotPasswordPage />
      </MemoryRouter>,
    );

    await userEvent.type(screen.getByLabelText(/email/i), "user@example.com");
    await userEvent.click(screen.getByRole("button", { name: /send reset link/i }));

    expect(forgotPasswordMock).toHaveBeenCalledWith({ email: "user@example.com" });
    expect(await screen.findByText(/password reset email has been prepared/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /open reset link/i })).toHaveAttribute(
      "href",
      "http://localhost:5173/reset-password?email=user%40example.com&token=abc",
    );
  }, 10000);
});
