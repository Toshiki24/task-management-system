export type NotificationType =
  | "MENTION"
  | "ASSIGNED"
  | "STATUS_CHANGED"
  | "COMMENT"
  | "DUE_SOON"
  | "DUE_OVERDUE";

export interface AppNotification {
  id: number;
  type: NotificationType;
  taskId: number | null;
  taskTitle: string | null;
  actorUserId: number | null;
  actorName: string | null;
  payload: Record<string, unknown> | null;
  isRead: boolean;
  createdAt: string;
}
