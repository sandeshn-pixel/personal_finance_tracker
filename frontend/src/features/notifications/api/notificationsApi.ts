import { apiClient } from "../../../shared/lib/api/client";

export type NotificationType = "RecurringDueReminder" | "RecurringExecutionFailed" | "GoalTargetApproaching" | "GoalCompleted" | "RuleTriggeredAlert" | "SharedAccountInvite";
export type NotificationLevel = "Info" | "Success" | "Warning";

export type NotificationDto = {
  id: string;
  type: NotificationType;
  level: NotificationLevel;
  title: string;
  message: string;
  route?: string | null;
  isRead: boolean;
  createdUtc: string;
  readAtUtc?: string | null;
};

export type NotificationFeedDto = {
  unreadCount: number;
  items: NotificationDto[];
};

export const notificationsApi = {
  list: (accessToken: string, unreadOnly = false, take = 8) => apiClient<NotificationFeedDto>(`/notifications?unreadOnly=${unreadOnly}&take=${take}`, { accessToken }),
  markRead: (accessToken: string, notificationId: string) => apiClient<void>(`/notifications/${notificationId}/read`, { method: "POST", accessToken }),
  markAllRead: (accessToken: string) => apiClient<void>("/notifications/read-all", { method: "POST", accessToken }),
};
