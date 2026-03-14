import type { PropsWithChildren, ReactNode } from "react";

export function ChartCard({ title, description, action, children }: PropsWithChildren<{ title: string; description: string; action?: ReactNode }>) {
  return (
    <section className="panel-card chart-card">
      <div className="panel-card__header panel-card__header--inline">
        <div>
          <h3>{title}</h3>
          <p>{description}</p>
        </div>
        {action ? <div>{action}</div> : null}
      </div>
      {children}
    </section>
  );
}
