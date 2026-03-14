import type { ReactNode } from "react";

export function EmptyState({ title, description, action }: { title: string; description: string; action?: ReactNode }) {
  return (
    <div className="empty-state">
      <div>
        <p className="eyebrow">No data yet</p>
        <h3>{title}</h3>
        <p>{description}</p>
      </div>
      {action ? <div>{action}</div> : null}
    </div>
  );
}
