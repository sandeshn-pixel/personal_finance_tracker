import { useEffect, useState } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
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
  const location = useLocation();
  const { user, logout } = useAuth();
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const initials = [user?.firstName?.charAt(0) ?? "", user?.lastName?.charAt(0) ?? ""].join("").toUpperCase();

  useEffect(() => {
    setIsSidebarOpen(false);
  }, [location.pathname]);

  async function handleLogout() {
    await logout();
    navigate("/login", { replace: true });
  }

  return (
    <div className={`shell-layout${isSidebarOpen ? " shell-layout--nav-open" : ""}`}>
      <button
        type="button"
        className={`sidebar-backdrop${isSidebarOpen ? " sidebar-backdrop--visible" : ""}`}
        aria-hidden={!isSidebarOpen}
        aria-label="Close navigation menu"
        onClick={() => setIsSidebarOpen(false)}
      />
      <aside className={`sidebar${isSidebarOpen ? " sidebar--open" : ""}`}>
        <div>
          <p className="eyebrow">Finance Tracker</p>
          <h2 className="sidebar-title">Ledger Nest</h2>
        </div>
        <nav id="primary-navigation" className="sidebar-nav" aria-label="Primary">
          {navigationItems.map((item) => (
            <NavLink key={item.to} to={item.to} className={({ isActive }) => isActive ? "nav-link nav-link--active" : "nav-link"} onClick={() => setIsSidebarOpen(false)}>
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-spacer" />
        <button type="button" className="ghost-button" onClick={handleLogout} aria-label="Log out">Logout</button>
        <div className="sidebar-footnote">Accounts, transactions, budgets, dashboard insights, and core reporting are now active.</div>
      </aside>
      <div className="shell-main">
        <header className="topbar">
          <div className="topbar-title-group">
            <button type="button" className="ghost-button mobile-nav-toggle" onClick={() => setIsSidebarOpen((current) => !current)} aria-label="Toggle navigation menu" aria-expanded={isSidebarOpen} aria-controls="primary-navigation">Menu</button>
            <div>
              <p className="eyebrow">Authenticated workspace</p>
              <h1 className="topbar-title">Welcome, {user?.firstName}</h1>
            </div>
          </div>
          <div className="topbar-actions">
            <div className="user-badge user-badge--avatar-only" aria-label="Signed-in user initials">
              <span className="user-badge__avatar" aria-hidden="true">{initials}</span>
            </div>
          </div>
        </header>
        <main className="content-panel"><Outlet /></main>
      </div>
    </div>
  );
}
