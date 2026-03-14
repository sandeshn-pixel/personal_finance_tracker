import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../app/providers/AuthProvider";

const navigationItems = [
  { to: "/dashboard", label: "Dashboard" },
  { to: "/transactions", label: "Transactions" },
  { to: "/accounts", label: "Accounts" },
  { to: "/budgets", label: "Budgets" },
  { to: "/goals", label: "Goals" },
  { to: "/reports", label: "Reports" },
  { to: "/recurring", label: "Recurring" },
  { to: "/settings", label: "Settings" },
];

export function AppShell() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  async function handleLogout() {
    await logout();
    navigate("/login", { replace: true });
  }

  return (
    <div className="shell-layout">
      <aside className="sidebar">
        <div>
          <p className="eyebrow">Finance Tracker</p>
          <h2 className="sidebar-title">Ledger Nest</h2>
        </div>
        <nav className="sidebar-nav" aria-label="Primary">
          {navigationItems.map((item) => (
            <NavLink key={item.to} to={item.to} className={({ isActive }) => isActive ? "nav-link nav-link--active" : "nav-link"}>
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-footnote">Accounts, transactions, budgets, dashboard insights, and core reporting are now active.</div>
      </aside>

      <div className="shell-main">
        <header className="topbar">
          <div>
            <p className="eyebrow">Authenticated workspace</p>
            <h1 className="topbar-title">Welcome, {user?.firstName}</h1>
          </div>
          <div className="topbar-actions">
            <div className="user-badge">
              <span>{user?.firstName} {user?.lastName}</span>
              <small>{user?.email}</small>
            </div>
            <button type="button" className="ghost-button" onClick={handleLogout}>Logout</button>
          </div>
        </header>
        <main className="content-panel"><Outlet /></main>
      </div>
    </div>
  );
}
