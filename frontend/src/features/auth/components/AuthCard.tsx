import { Link } from "react-router-dom";
import type { ReactNode } from "react";

export function AuthCard({
  title,
  subtitle,
  footer,
  children,
}: {
  title: string;
  subtitle: string;
  footer: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="auth-shell">
      <section className="auth-panel auth-panel--hero">
        <div className="brand-mark">FT</div>
        <p className="eyebrow">Personal Finance Tracker</p>
        <h1>{title}</h1>
        <p className="auth-copy">{subtitle}</p>
        <h3>Manage Your Personal Finances</h3>
        <img 
          src="/public/Personal_finance.png"  
          alt="Personal Finance Tracker Illustration"  
          className="hero-image" 
          style={{  width: '630px', height: 'auto' }} 
        />
        <p> 
          Track expenses, set budgets, and achieve your financial goals
        </p>
      </section>

      <section className="auth-panel auth-panel--form">
        <div className="auth-form-header">
          <Link to="/" className="wordmark">Ledger Nest</Link>
        </div>
        {children}
        <div className="auth-footer">{footer}</div>
      </section>
    </div>
  );
}
