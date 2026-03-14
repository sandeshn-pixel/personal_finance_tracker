import type { ButtonHTMLAttributes, PropsWithChildren } from "react";

export function Button({ children, loading, ...props }: PropsWithChildren<ButtonHTMLAttributes<HTMLButtonElement> & { loading?: boolean }>) {
  return (
    <button className="primary-button" {...props} disabled={props.disabled || loading}>
      {loading ? "Please wait..." : children}
    </button>
  );
}
