import type { PropsWithChildren } from "react";

export function Field({ label, error, hint, children }: PropsWithChildren<{ label: string; error?: string; hint?: string }>) {
  return (
    <label className="field">
      <span className="field-label">{label}</span>
      {children}
      {error ? <span className="field-error">{error}</span> : hint ? <span className="field-hint">{hint}</span> : null}
    </label>
  );
}
