export function PlaceholderPage({ title, description }: { title: string; description: string }) {
  return (
    <section className="placeholder-page">
      <div className="placeholder-card">
        <p className="eyebrow">Milestone One</p>
        <h2>{title}</h2>
        <p>{description}</p>
      </div>
      <div className="placeholder-grid">
        <article className="metric-card">
          <span>Status</span>
          <strong>App shell ready</strong>
          <p>Protected routing and authenticated layout are in place.</p>
        </article>
        <article className="metric-card">
          <span>Security</span>
          <strong>Cookie-backed session</strong>
          <p>Refresh token handling is server-managed and rotated on every refresh.</p>
        </article>
        <article className="metric-card">
          <span>Next</span>
          <strong>Domain modules</strong>
          <p>Transactions, budgets, and reporting can plug into this shell next.</p>
        </article>
      </div>
    </section>
  );
}
