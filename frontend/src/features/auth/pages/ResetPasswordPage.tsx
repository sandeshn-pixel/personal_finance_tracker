import { useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Link, useSearchParams } from "react-router-dom";
import { z } from "zod";
import { authApi } from "../api/authApi";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { Field } from "../../../shared/components/Field";
import { PasswordField } from "../../../shared/components/PasswordField";
import { ApiError } from "../../../shared/lib/api/client";
import { AuthCard } from "../components/AuthCard";

const resetPasswordSchema = z.object({
  newPassword: z.string()
    .min(12, "Password must be at least 12 characters.")
    .regex(/[A-Z]/, "Include at least one uppercase letter.")
    .regex(/[a-z]/, "Include at least one lowercase letter.")
    .regex(/[0-9]/, "Include at least one number.")
    .regex(/[^a-zA-Z0-9]/, "Include at least one special character."),
  confirmPassword: z.string().min(1, "Confirm your password."),
}).refine((value) => value.newPassword === value.confirmPassword, {
  path: ["confirmPassword"],
  message: "Passwords do not match.",
});

type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;

export function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const email = searchParams.get("email")?.trim() ?? "";
  const token = searchParams.get("token")?.trim() ?? "";
  const hasResetContext = useMemo(() => email.length > 0 && token.length > 0, [email, token]);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: "", confirmPassword: "" },
  });
  const newPasswordField = register("newPassword");
  const confirmPasswordField = register("confirmPassword");

  async function onSubmit(values: ResetPasswordFormValues) {
    if (!hasResetContext) {
      setErrorMessage("This reset link is incomplete. Request a new one.");
      return;
    }

    setErrorMessage(null);
    setSuccessMessage(null);

    try {
      await authApi.resetPassword({ email, token, newPassword: values.newPassword });
      setSuccessMessage("Password updated. You can sign in with your new password now.");
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(error.message);
        return;
      }

      setErrorMessage("Unable to reset password. Please request a new link.");
    }
  }

  return (
    <AuthCard
      title="Choose a new password"
      subtitle="Use a strong password. Reset links are one-time and automatically expire for safety."
      footer={<p>Need a new link? <Link to="/forgot-password">Request another reset</Link></p>}
    >
      <form className="form-stack" onSubmit={handleSubmit(onSubmit)} noValidate>
        {!hasResetContext ? <Alert message="This reset link is missing required details. Request a fresh password reset link." /> : null}
        {errorMessage ? <Alert message={errorMessage} /> : null}
        {successMessage ? <Alert message={successMessage} /> : null}
        <Field label="Email">
          <input value={email} disabled readOnly />
        </Field>
        <Field label="New password" error={errors.newPassword?.message} hint="Use 12+ characters with upper, lower, number, and symbol.">
          <PasswordField {...newPasswordField} inputRef={newPasswordField.ref} autoComplete="new-password" placeholder="Create a strong password" toggleLabel="new password" />
        </Field>
        <Field label="Confirm password" error={errors.confirmPassword?.message}>
          <PasswordField {...confirmPasswordField} inputRef={confirmPasswordField.ref} autoComplete="new-password" placeholder="Re-enter your password" toggleLabel="password confirmation" />
        </Field>
        <Button type="submit" loading={isSubmitting} disabled={!hasResetContext}>Reset password</Button>
        {successMessage ? <Link to="/login">Back to sign in</Link> : null}
      </form>
    </AuthCard>
  );
}
