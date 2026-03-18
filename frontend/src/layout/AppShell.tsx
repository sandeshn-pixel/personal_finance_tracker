import { useEffect, useState } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../app/providers/AuthProvider";
import { notificationsApi, type NotificationDto, type NotificationFeedDto } from "../features/notifications/api/notificationsApi";
import { ApiError } from "../shared/lib/api/client";
import { formatDate } from "../shared/lib/format";

const navigationItems = [
  { to: "/dashboard", label: "Dashboard" },
  { to: "/transactions", label: "Transactions" },
  { to: "/accounts", label: "Accounts" },
  { to: "/categories", label: "Categories" },
  { to: "/budgets", label: "Budgets" },
  { to: "/goals", label: "Goals" },
  { to: "/reports", label: "Reports" },
  { to: "/recurring", label: "Recurring" },
  { to: "/notifications", label: "Notifications" },
  { to: "/settings", label: "Settings" },
];

export function AppShell() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, accessToken, logout } = useAuth();
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false);
  const [notificationFeed, setNotificationFeed] = useState<NotificationFeedDto>({ unreadCount: 0, items: [] });
  const [notificationError, setNotificationError] = useState<string | null>(null);
  const [loadingNotifications, setLoadingNotifications] = useState(false);
  const initials = [user?.firstName?.charAt(0) ?? "", user?.lastName?.charAt(0) ?? ""].join("").toUpperCase();

  useEffect(() => {
    setIsSidebarOpen(false);
    setIsNotificationsOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!accessToken) {
      setNotificationFeed({ unreadCount: 0, items: [] });
      return;
    }

    const token = accessToken;
    let cancelled = false;
    let intervalId: number | undefined;

    async function loadNotifications() {
      setLoadingNotifications(true);
      try {
        const feed = await notificationsApi.list(token, false, 8);
        if (!cancelled) {
          setNotificationFeed(feed);
          setNotificationError(null);
        }
      } catch (error) {
        if (!cancelled) {
          setNotificationError(error instanceof ApiError ? error.message : "Unable to load notifications.");
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

    if (!isNotificationsOpen) {
      setLoadingNotifications(true);
      try {
        const feed = await notificationsApi.list(accessToken, false, 8);
        setNotificationFeed(feed);
        setNotificationError(null);
      } catch (error) {
        setNotificationError(error instanceof ApiError ? error.message : "Unable to load notifications.");
      } finally {
        setLoadingNotifications(false);
      }
    }

    setIsNotificationsOpen((current) => !current);
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
            <button type="button" className="ghost-button mobile-nav-toggle" onClick={() => setIsSidebarOpen((current) => !current)} aria-label="Toggle navigation menu" aria-expanded={isSidebarOpen} aria-controls="primary-navigation">Menu</button>
            <div>
              <p className="eyebrow">Authenticated workspace</p>
              <h1 className="topbar-title">Welcome, {user?.firstName}</h1>
            </div>
          </div>
          <div className="topbar-actions">
            <div className="notification-shell">
              <button type="button" className="notification-button" onClick={() => void toggleNotifications()} aria-haspopup="dialog" aria-expanded={isNotificationsOpen} aria-label="Open notifications">
                <span aria-hidden="true">Alerts</span>
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
            <button type="button" className="user-badge user-badge--avatar-only user-badge-button" aria-label="Open settings" onClick={() => navigate("/settings") }>
              <span className="user-badge__avatar" aria-hidden="true">{initials}</span>
            </button>
          </div>
        </header>
        <main className="content-panel"><Outlet /></main>
      </div>
    </div>
  );
}




