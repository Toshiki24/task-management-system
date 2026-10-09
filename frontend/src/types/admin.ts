/** 管理コンソール(System Admin 専用、M5 §5)の型。 */

export interface SystemStats {
  workspaceCount: number;
  projectCount: number;
  taskCount: number;
  userCount: number;
  systemAdminCount: number;
  recentActivityCount: number;
}

export interface AdminWorkspace {
  id: number;
  name: string;
  memberCount: number;
  projectCount: number;
  isArchived: boolean;
  createdAt: string;
}

export interface AdminUser {
  id: number;
  name: string;
  email: string;
  isSystemAdmin: boolean;
  createdAt: string;
}

export interface AuditLog {
  id: number;
  actorUserId: number;
  actorName: string | null;
  action: string;
  targetType: string;
  targetId: number;
  workspaceId: number | null;
  metadata: string | null;
  createdAt: string;
}

export interface AuditLogPage {
  items: AuditLog[];
  total: number;
}
