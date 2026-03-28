import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Link } from "react-router-dom";
import { z } from "zod";
import { authApi, type ForgotPasswordResponse } from "../api/authApi";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { Field } from "../../../shared/components/Field";
import { ApiError } from "../../../shared/lib/api/client";
import { AuthCard } from "../components/AuthCard";

const forgotPasswordSchema = z.object({
  email: z.string().trim().email("Enter a valid email address."),
});

type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;

export function ForgotPasswordPage() {
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [response, setResponse] = useState<ForgotPasswordResponse | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: "" },
  });

  async function onSubmit(values: ForgotPasswordFormValues) {
    setErrorMessage(null);

    try {
      const result = await authApi.forgotPassword(values);
      setResponse(result);
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(error.message);
        return;
      }

      setErrorMessage("Unable to start password reset. Please try again.");
    }
  }

  return (
    <AuthCard
      title="Reset your password"
      subtitle="Request a secure one-time reset link. If email delivery is configured, the link will be sent to your inbox. Local development can still show a direct shortcut when SMTP is disabled."
      footer={<p>Remembered it? <Link to="/login">Back to sign in</Link></p>}
    >
      <form className="form-stack" onSubmit={handleSubmit(onSubmit)} noValidate>
        {errorMessage ? <Alert message={errorMessage} /> : null}
        {response ? <Alert message={response.message} /> : null}
        {response?.debugStatus ? <Alert message={response.debugStatus} variant="info" /> : null}
        <Field label="Email" error={errors.email?.message} hint="We will never reveal whether this email exists. In development, a direct reset link may appear if mail delivery is turned off.">
          <input {...register("email")} type="email" autoComplete="email" placeholder="name@company.com" />
        </Field>
        <Button type="submit" loading={isSubmitting}>Send reset link</Button>
        {response?.resetUrl ? (
          <div className="auth-inline-actions">
            <a href={response.resetUrl}>Open reset link</a>
            <span className="field-hint">Development-only shortcut</span>
          </div>
        ) : null}
      </form>
    </AuthCard>
  );
}
