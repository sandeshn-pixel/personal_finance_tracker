import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../../../app/providers/AuthProvider";
import { Alert } from "../../../shared/components/Alert";
import { EmptyState } from "../../../shared/components/EmptyState";
import { PageLoader } from "../../../shared/components/PageLoader";
import { SectionHeader } from "../../../shared/components/SectionHeader";
import { StatCard } from "../../../shared/components/StatCard";
import { ApiError } from "../../../shared/lib/api/client";
import { formatDate } from "../../../shared/lib/format";
import { notificationsApi, type NotificationDto, type NotificationFeedDto } from "../api/notificationsApi";

export function NotificationsPage() {
  const navigate = useNavigate();
  const { accessToken } = useAuth();
  const [feed, setFeed] = useState<NotificationFeedDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => { void load(unreadOnly); }, [accessToken, unreadOnly]);

  async function load(nextUnreadOnly: boolean) {
    if (!accessToken) return;
    setLoading(true);
    try {
      const response = await notificationsApi.list(accessToken, nextUnreadOnly, 50);
      setFeed(response);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(error instanceof ApiError ? error.message : "Unable to load notifications.");
    } finally {
      setLoading(false);
    }
  }

  async function openNotification(notification: NotificationDto) {
    if (!accessToken) {
      return;
    }

    if (!notification.isRead) {
      await notificationsApi.markRead(accessToken, notification.id);
      setFeed((current) => current ? {
        unreadCount: Math.max(current.unreadCount - 1, 0),
        items: current.items.map((item) => item.id === notification.id ? { ...item, isRead: true, readAtUtc: new Date().toISOString() } : item),
      } : current);
    }

    if (notification.route) {
      navigate(notification.route);
    }
  }

  async function markAllRead() {
    if (!accessToken || !feed || feed.unreadCount === 0) {
      return;
    }

    await notificationsApi.markAllRead(accessToken);
    setFeed((current) => current ? {
      unreadCount: 0,
      items: current.items.map((item) => ({ ...item, isRead: true, readAtUtc: item.readAtUtc ?? new Date().toISOString() })),
    } : current);
  }

  const inviteItems = useMemo(
    () => feed?.items.filter((item) => item.type === "SharedAccountInvite") ?? [],
    [feed],
  );

  const unreadItems = useMemo(
    () => feed?.items.filter((item) => !item.isRead) ?? [],
    [feed],
  );

  const generalItems = useMemo(
    () => feed?.items.filter((item) => item.type !== "SharedAccountInvite") ?? [],
    [feed],
  );

  if (loading && !feed) return <PageLoader label="Loading notifications" />;
  if (!feed && errorMessage) return <Alert message={errorMessage} />;

  return (
    <div className="page-stack notifications-page">
      <SectionHeader
        title="Notifications"
        description="Review reminders, automation outcomes, shared-account invites, and goal updates in one place."
        action={(
          <div className="button-row">
            <button type="button" className={`ghost-button${!unreadOnly ? " notifications-filter-button--active" : ""}`} onClick={() => setUnreadOnly(false)}>All</button>
            <button type="button" className={`ghost-button${unreadOnly ? " notifications-filter-button--active" : ""}`} onClick={() => setUnreadOnly(true)}>Unread only</button>
            <button type="button" className="ghost-button" onClick={() => void markAllRead()} disabled={!feed || feed.unreadCount === 0}>Mark all read</button>
          </div>
        )}
      />
      {errorMessage ? <Alert message={errorMessage} /> : null}

      <div className="stats-grid stats-grid--three notifications-summary-grid">
        <StatCard
          label="Unread now"
          value={String(feed?.unreadCount ?? 0)}
          hint={unreadOnly ? "Unread-only view is active." : "Unread items across your current feed."}
          tone={(feed?.unreadCount ?? 0) > 0 ? "positive" : undefined}
        />
        <StatCard
          label="Invite updates"
          value={String(inviteItems.length)}
          hint="Shared-account invite events in this feed."
        />
        <StatCard
          label="Loaded items"
          value={String(feed?.items.length ?? 0)}
          hint="Notifications currently visible in this page view."
        />
      </div>

      <section className="panel-card notifications-history-card">
        <div className="panel-card__header panel-card__header--inline">
          <div>
            <h3>My invites</h3>
            <p>Invite activity and account-sharing updates that deserve quick review.</p>
          </div>
          <button type="button" className="ghost-button ghost-button--small" onClick={() => void load(unreadOnly)}>Refresh</button>
        </div>

        {loading ? <p className="notification-panel__state">Refreshing notifications...</p> : null}
        {!loading && inviteItems.length === 0 ? (
          <EmptyState title="No invites right now" description={unreadOnly ? "No unread invite notifications are waiting for you." : "Shared-account invites sent to you will appear here."} />
        ) : null}

        <div className="notification-list notification-list--page">
          {inviteItems.map((notification) => (
            <button
              key={notification.id}
              type="button"
              className={`notification-item notification-item--history${notification.isRead ? " notification-item--read" : ""}`}
              onClick={() => void openNotification(notification)}
            >
              <span className={`notification-dot notification-dot--${notification.level.toLowerCase()}`} aria-hidden="true" />
              <span className="notification-item__content">
                <span className="notification-item__meta">
                  <small>{formatDate(notification.createdUtc)}</small>
                  <small>{notification.route ? "Opens invite flow" : "In-app update"}</small>
                </span>
                <strong>{notification.title}</strong>
                <span>{notification.message}</span>
              </span>
              <span className={`status-badge ${notification.isRead ? "status-badge--warning" : "status-badge--default"}`}>{notification.isRead ? "Read" : "New"}</span>
            </button>
          ))}
        </div>
      </section>

      <section className="panel-card notifications-history-card">
        <div className="panel-card__header panel-card__header--inline">
          <div>
            <h3>{unreadOnly ? "Unread activity" : "Notification history"}</h3>
            <p>{unreadOnly ? `${unreadItems.length} unread notification${unreadItems.length === 1 ? "" : "s"} still need review.` : "A chronological view of reminders, automation outcomes, and progress updates."}</p>
          </div>
          <button type="button" className="ghost-button ghost-button--small" onClick={() => void load(unreadOnly)}>Refresh</button>
        </div>

        {loading ? <p className="notification-panel__state">Refreshing notifications...</p> : null}
        {!loading && generalItems.length === 0 ? (
          <EmptyState title="No other notifications to show" description={unreadOnly ? "Everything else is read right now." : "You are all caught up for now."} />
        ) : null}

        <div className="notification-list notification-list--page">
          {generalItems.map((notification) => (
            <button
              key={notification.id}
              type="button"
              className={`notification-item notification-item--history${notification.isRead ? " notification-item--read" : ""}`}
              onClick={() => void openNotification(notification)}
            >
              <span className={`notification-dot notification-dot--${notification.level.toLowerCase()}`} aria-hidden="true" />
              <span className="notification-item__content">
                <span className="notification-item__meta">
                  <small>{formatDate(notification.createdUtc)}</small>
                  <small>{notification.route ? `Opens ${notification.route}` : "No linked route"}</small>
                </span>
                <strong>{notification.title}</strong>
                <span>{notification.message}</span>
              </span>
              <span className={`status-badge ${notification.isRead ? "status-badge--warning" : "status-badge--default"}`}>{notification.isRead ? "Read" : "New"}</span>
            </button>
          ))}
        </div>
      </section>
    </div>
  );
}
