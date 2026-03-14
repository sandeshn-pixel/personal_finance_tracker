import type { PropsWithChildren, ReactNode } from "react";

export function FilterRow({ action, children }: PropsWithChildren<{ action?: ReactNode }>) {
  return (
    <div className="filter-row">
      <div className="filter-row__inputs">{children}</div>
      {action ? <div className="filter-row__action">{action}</div> : null}
    </div>
  );
}
