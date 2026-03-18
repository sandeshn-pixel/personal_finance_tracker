import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router-dom";
import { z } from "zod";
import { useAuth } from "../../../app/providers/AuthProvider";
import { Alert } from "../../../shared/components/Alert";
import { Button } from "../../../shared/components/Button";
import { Field } from "../../../shared/components/Field";
import { ApiError } from "../../../shared/lib/api/client";
import { AuthCard } from "../components/AuthCard";

const signupSchema = z
  .object({
    firstName: z.string().trim().min(1, "First name is required.").max(100, "First name is too long."),
    lastName: z.string().trim().min(1, "Last name is required.").max(100, "Last name is too long."),
    email: z.string().trim().email("Enter a valid email address."),
    password: z
      .string()
      .min(8, "Password must be at least 8 characters.")
      .regex(/[A-Z]/, "Include at least one uppercase letter.")
      .regex(/[a-z]/, "Include at least one lowercase letter.")
      .regex(/[0-9]/, "Include at least one number.")
      .regex(/[^a-zA-Z0-9]/, "Include at least one special character."),
    confirmPassword: z.string().min(1, "Confirm your password."),
  })
  .refine((value) => value.password === value.confirmPassword, {
    path: ["confirmPassword"],
    message: "Passwords do not match.",
  });

type SignupFormValues = z.infer<typeof signupSchema>;

export function SignupPage() {
  const navigate = useNavigate();
  const { signup } = useAuth();
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<SignupFormValues>({
    resolver: zodResolver(signupSchema),
    defaultValues: {
      firstName: "",
      lastName: "",
      email: "",
      password: "",
      confirmPassword: "",
    },
  });

  async function onSubmit(values: SignupFormValues) {
    setErrorMessage(null);

    try {
      await signup({
        firstName: values.firstName,
        lastName: values.lastName,
        email: values.email,
        password: values.password,
      });
      navigate("/dashboard", { replace: true });
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(error.message);
        return;
      }

      setErrorMessage("Registration failed. Please try again.");
    }
  }

  return (
    <AuthCard
      title="Create your workspace"
      subtitle="Start with a hardened account layer and a clean shell for the finance modules that follow."
      footer={
        <p>
          Already have an account? <Link to="/login">Sign in</Link>
        </p>
      }
    >
      <form className="form-stack" onSubmit={handleSubmit(onSubmit)} noValidate>
        {errorMessage ? <Alert message={errorMessage} /> : null}
        <div className="field-grid">
          <Field label="First name" error={errors.firstName?.message}>
            <input {...register("firstName")} autoComplete="given-name" placeholder="Sandesh" />
          </Field>
          <Field label="Last name" error={errors.lastName?.message}>
            <input {...register("lastName")} autoComplete="family-name" placeholder="Nagaraj" />
          </Field>
        </div>
        <Field label="Email" error={errors.email?.message}>
          <input {...register("email")} type="email" autoComplete="email" placeholder="name@company.com" />
        </Field>
        <Field label="Password" error={errors.password?.message} hint="Use 12+ characters with upper, lower, number, and symbol.">
          <input {...register("password")} type="password" autoComplete="new-password" placeholder="Create a strong password" />
        </Field>
        <Field label="Confirm password" error={errors.confirmPassword?.message}>
          <input {...register("confirmPassword")} type="password" autoComplete="new-password" placeholder="Re-enter your password" />
        </Field>
        <Button type="submit" loading={isSubmitting}>Create account</Button>
      </form>
    </AuthCard>
  );
}
