import { forwardRef } from "react";
import type { PropsWithChildren, SelectHTMLAttributes } from "react";

export const SelectField = forwardRef<HTMLSelectElement, PropsWithChildren<SelectHTMLAttributes<HTMLSelectElement>>>(function SelectField(
  { children, ...props },
  ref,
) {
  return <select ref={ref} className="select-input" {...props}>{children}</select>;
});
