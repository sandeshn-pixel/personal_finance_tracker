import { useEffect, useState } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../app/providers/AuthProvider";
import { useWorkspaceScope } from "../app/providers/WorkspaceScopeProvider";
import { accountsApi } from "../features/accounts/api/accountsApi";
import { notificationsApi, type NotificationDto, type NotificationFeedDto } from "../features/notifications/api/notificationsApi";
import { ApiError } from "../shared/lib/api/client";
import { formatDate } from "../shared/lib/format";
import { hasSharedGuestAccounts } from "../shared/lib/sharedAccessView";
import { WorkspaceScopeSelect } from "../shared/components/WorkspaceScopeSelect";

const navigationItems = [
  { to: "/dashboard", label: "Dashboard" },
  { to: "/transactions", label: "Transactions" },
  { to: "/accounts", label: "Accounts" },
  { to: "/categories", label: "Categories" },
  { to: "/budgets", label: "Budgets" },
  { to: "/goals", label: "Goals" },
  { to: "/insights", label: "Insights" },
  { to: "/rules", label: "Rules" },
  { to: "/reports", label: "Reports" },
  { to: "/recurring", label: "Recurring" },
  { to: "/notifications", label: "Notifications" },
  { to: "/settings", label: "Settings" },
];

export function AppShell() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, accessToken, logout } = useAuth();
  const { sharedAccessView, setSharedAccessView } = useWorkspaceScope();
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false);
  const [notificationFeed, setNotificationFeed] = useState<NotificationFeedDto>({ unreadCount: 0, items: [] });
  const [notificationError, setNotificationError] = useState<string | null>(null);
  const [loadingNotifications, setLoadingNotifications] = useState(false);
  const [ownedPendingInviteCount, setOwnedPendingInviteCount] = useState(0);
  const [firstPendingInviteAccountId, setFirstPendingInviteAccountId] = useState<string | null>(null);
  const [workspaceAccounts, setWorkspaceAccounts] = useState<Array<{ id: string; pendingInviteCount: number; isShared: boolean; currentUserRole: string }>>([]);
  const initials = [user?.firstName?.charAt(0) ?? "", user?.lastName?.charAt(0) ?? ""].join("").toUpperCase();

  useEffect(() => {
    setIsSidebarOpen(false);
    setIsNotificationsOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!accessToken) {
      setNotificationFeed({ unreadCount: 0, items: [] });
      setOwnedPendingInviteCount(0);
      setWorkspaceAccounts([]);
      setFirstPendingInviteAccountId(null);
      return;
    }

    const token = accessToken;
    let cancelled = false;
    let intervalId: number | undefined;

    async function loadNotifications() {
      setLoadingNotifications(true);
      try {
        const [feedResult, accountsResult] = await Promise.allSettled([
          notificationsApi.list(token, false, 8),
          accountsApi.list(token),
        ]);

        if (!cancelled) {
          if (feedResult.status === "fulfilled") {
            setNotificationFeed(feedResult.value);
            setNotificationError(null);
          } else {
            const error = feedResult.reason;
            setNotificationError(error instanceof ApiError ? error.message : "Unable to load notifications.");
          }

          if (accountsResult.status === "fulfilled") {
            const accounts = accountsResult.value;
            setWorkspaceAccounts(accounts);
            setOwnedPendingInviteCount(accounts.reduce((sum, item) => sum + item.pendingInviteCount, 0));
            setFirstPendingInviteAccountId(accounts.find((item) => item.pendingInviteCount > 0)?.id ?? null);
          }
        }
      } finally {
        if (!cancelled) {
          setLoadingNotifications(false);
        }
      }
    }

    void loadNotifications();
    intervalId = window.setInterval(() => { void loadNotifications(); }, 60000);

    return () => {
      cancelled = true;
      if (intervalId) {
        window.clearInterval(intervalId);
      }
    };
  }, [accessToken]);

  async function handleLogout() {
    await logout();
    navigate("/login", { replace: true });
  }

  async function toggleNotifications() {
    if (!accessToken) {
      return;
    }

    if (isNotificationsOpen) {
      setIsNotificationsOpen(false);
      return;
    }

    setLoadingNotifications(true);
    try {
      const [feedResult, accountsResult] = await Promise.allSettled([
        notificationsApi.list(accessToken, false, 8),
        accountsApi.list(accessToken),
      ]);

      if (feedResult.status === "fulfilled") {
        const nextFeed = feedResult.value;
        if (nextFeed.unreadCount > 0) {
          try {
            await notificationsApi.markAllRead(accessToken);
            setNotificationFeed({
              unreadCount: 0,
              items: nextFeed.items.map((item) => ({ ...item, isRead: true, readAtUtc: item.readAtUtc ?? new Date().toISOString() })),
            });
          } catch {
            setNotificationFeed(nextFeed);
            setNotificationError("Notifications opened, but read state could not be updated right now.");
          }
        } else {
          setNotificationFeed(nextFeed);
        }
        setNotificationError(null);
      } else {
        const error = feedResult.reason;
        setNotificationError(error instanceof ApiError ? error.message : "Unable to load notifications.");
      }

      if (accountsResult.status === "fulfilled") {
        const accounts = accountsResult.value;
        setWorkspaceAccounts(accounts);
        setOwnedPendingInviteCount(accounts.reduce((sum, item) => sum + item.pendingInviteCount, 0));
        setFirstPendingInviteAccountId(accounts.find((item) => item.pendingInviteCount > 0)?.id ?? null);
      }

      setIsNotificationsOpen(true);
    } finally {
      setLoadingNotifications(false);
    }
  }

  async function markAllNotificationsRead() {
    if (!accessToken || notificationFeed.unreadCount === 0) {
      return;
    }

    await notificationsApi.markAllRead(accessToken);
    setNotificationFeed((current) => ({
      unreadCount: 0,
      items: current.items.map((item) => ({ ...item, isRead: true, readAtUtc: item.readAtUtc ?? new Date().toISOString() })),
    }));
  }

  async function handleNotificationClick(notification: NotificationDto) {
    if (!accessToken) {
      return;
    }

    if (!notification.isRead) {
      await notificationsApi.markRead(accessToken, notification.id);
      setNotificationFeed((current) => ({
        unreadCount: Math.max(current.unreadCount - 1, 0),
        items: current.items.map((item) => item.id === notification.id ? { ...item, isRead: true, readAtUtc: new Date().toISOString() } : item),
      }));
    }

    setIsNotificationsOpen(false);
    if (notification.route) {
      navigate(notification.route);
    }
  }

  const showWorkspaceScopeToggle = hasSharedGuestAccounts(workspaceAccounts);

  useEffect(() => {
    if (!showWorkspaceScopeToggle && sharedAccessView !== "all") {
      setSharedAccessView("all");
    }
  }, [setSharedAccessView, sharedAccessView, showWorkspaceScopeToggle]);

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
        <button type="button" className="ghost-button sidebar-logout-button" onClick={handleLogout} aria-label="Log out">Logout</button>
      </aside>
      <div className="shell-main">
        <header className="topbar">
          <div className="topbar-title-group">
            <button type="button" className="ghost-button mobile-nav-toggle" onClick={() => setIsSidebarOpen((current) => !current)} aria-label="Toggle navigation menu" aria-expanded={isSidebarOpen} aria-controls="primary-navigation">
              <span className="mobile-nav-toggle__icon" aria-hidden="true">
                <span />
                <span />
                <span />
              </span>
            </button>
            <div>
              <p className="eyebrow">Authenticated workspace</p>
              <h1 className="topbar-title">Welcome, {user?.firstName}</h1>
            </div>
          </div>
          <div className="topbar-actions">
            {showWorkspaceScopeToggle ? (
              <div className="topbar-scope-wrap">
                <WorkspaceScopeSelect
                  value={sharedAccessView}
                  onChange={setSharedAccessView}
                  className="topbar-scope-toggle"
                  label="Workspace"
                />
              </div>
            ) : null}
            <div className="notification-shell">
              <button
                type="button"
                className={`notification-button${ownedPendingInviteCount > 0 ? " notification-button--attention" : ""}`}
                onClick={() => void toggleNotifications()}
                aria-haspopup="dialog"
                aria-expanded={isNotificationsOpen}
                aria-label="Open notifications"
              >
                <span className="notification-button__icon" aria-hidden="true">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M15 17h5l-1.4-1.4A2 2 0 0 1 18 14.2V11a6 6 0 1 0-12 0v3.2a2 2 0 0 1-.6 1.4L4 17h5" />
                    <path d="M10 20a2 2 0 0 0 4 0" />
                  </svg>
                </span>
                {notificationFeed.unreadCount > 0 ? <span className="notification-count">{notificationFeed.unreadCount}</span> : null}
              </button>
              {isNotificationsOpen ? (
                <div className="notification-panel" role="dialog" aria-label="Notifications">
                  <div className="notification-panel__header">
                    <div>
                      <strong>Notifications</strong>
                      <p>{notificationFeed.unreadCount} unread</p>
                    </div>
                    <button type="button" className="ghost-button ghost-button--small" onClick={() => void markAllNotificationsRead()} disabled={notificationFeed.unreadCount === 0}>Mark all read</button>
                  </div>
                  {ownedPendingInviteCount > 0 ? (
                    <div className="notification-panel__summary">
                      <strong>Sharing actions waiting</strong>
                      <p>You have {ownedPendingInviteCount} pending shared-account invite{ownedPendingInviteCount === 1 ? "" : "s"} across your owned accounts.</p>
                      <button type="button" className="ghost-button ghost-button--small" onClick={() => { setIsNotificationsOpen(false); navigate(firstPendingInviteAccountId ? `/accounts/${firstPendingInviteAccountId}#sharing` : "/accounts"); }}>Review sharing</button>
                    </div>
                  ) : null}
                  <div className="notification-panel__actions">
                    <button type="button" className="ghost-button ghost-button--small" onClick={() => { setIsNotificationsOpen(false); navigate("/notifications"); }}>View all</button>
                  </div>
                  {notificationError ? <p className="notification-panel__state">{notificationError}</p> : null}
                  {loadingNotifications ? <p className="notification-panel__state">Loading notifications...</p> : null}
                  {!loadingNotifications && notificationFeed.items.length === 0 ? <p className="notification-panel__state">No notifications right now.</p> : null}
                  <div className="notification-list">
                    {notificationFeed.items.map((notification) => (
                      <button key={notification.id} type="button" className={`notification-item${notification.isRead ? " notification-item--read" : ""}`} onClick={() => void handleNotificationClick(notification)}>
                        <span className={`notification-dot notification-dot--${notification.level.toLowerCase()}`} aria-hidden="true" />
                        <span className="notification-item__content">
                          <strong>{notification.title}</strong>
                          <span>{notification.message}</span>
                          <small>{formatDate(notification.createdUtc)}</small>
                        </span>
                      </button>
                    ))}
                  </div>
                </div>
              ) : null}
            </div>
            <button type="button" className="user-badge user-badge--avatar-only user-badge-button" aria-label="Open settings" onClick={() => navigate("/settings")}>
              <span className="user-badge__avatar" aria-hidden="true">{initials}</span>
            </button>
          </div>
        </header>
        <main className="content-panel"><Outlet /></main>
      </div>
    </div>
  );
}





