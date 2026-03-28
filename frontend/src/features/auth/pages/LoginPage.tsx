import { useMemo, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from "react-router-dom";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { Field } from "../../../shared/components/Field";
import { PasswordField } from "../../../shared/components/PasswordField";
import { ApiError } from "../../../shared/lib/api/client";
import { AuthCard } from "../components/AuthCard";

const loginSchema = z.object({
  email: z.string().trim().email("Enter a valid email address."),
  password: z.string().min(1, "Password is required."),
});

type LoginFormValues = z.infer<typeof loginSchema>;

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const { login, status } = useAuth();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const state = location.state as { from?: { pathname?: string; search?: string; hash?: string } } | null;
  const redirectParam = searchParams.get("redirect");
  const from = useMemo(() => {
    if (redirectParam && redirectParam.startsWith("/")) {
      return redirectParam;
    }

    if (state?.from?.pathname) {
      return `${state.from.pathname}${state.from.search ?? ""}${state.from.hash ?? ""}`;
    }

    return "/dashboard";
  }, [redirectParam, state]);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: "",
      password: "",
    },
  });
  const passwordField = register("password");

  async function onSubmit(values: LoginFormValues) {
    setErrorMessage(null);

    try {
      await login(values);
      navigate(from, { replace: true });
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(error.message);
        return;
      }

      setErrorMessage("Sign in failed. Please try again.");
    }
  }

  if (status === "authenticated") {
    return <Navigate to={from} replace />;
  }

  const signupLink = redirectParam ? `/signup?redirect=${encodeURIComponent(redirectParam)}` : "/signup";

  return (
    <AuthCard
      title="Welcome back"
      subtitle="Sign in to continue into the secure foundation of your finance workspace."
      footer={
        <p>
          New here? <Link to={signupLink}>Create your account</Link>
        </p>
      }
    >
      <form className="form-stack" onSubmit={handleSubmit(onSubmit)} noValidate>
        {errorMessage ? <Alert message={errorMessage} /> : null}
        <Field label="Email" error={errors.email?.message}>
          <input {...register("email")} type="email" autoComplete="email" placeholder="name@company.com" />
        </Field>
        <Field label="Password" error={errors.password?.message}>
          <PasswordField {...passwordField} inputRef={passwordField.ref} autoComplete="current-password" placeholder="Enter your password" toggleLabel="password" />
        </Field>
        <div className="auth-inline-actions">
          <Link to="/forgot-password">Forgot password?</Link>
        </div>
        <Button type="submit" loading={isSubmitting}>Sign in</Button>
      </form>
    </AuthCard>
  );
}
