export type WorkspaceRole = "ADMIN" | "MEMBER" | "VIEWER";

export interface Workspace {
  id: number;
  name: string;
  description: string | null;
  isArchived: boolean;
  // 呼び出しユーザーのそのワークスペースでのロール(System Admin が非所属の場合は null)
  myRole: WorkspaceRole | null;
  createdAt: string;
}

export interface WorkspaceMember {
  userId: number;
  name: string;
  email: string;
  role: WorkspaceRole;
}

export interface AddWorkspaceMemberBody {
  userId: number;
  role: WorkspaceRole;
}

export interface UpdateWorkspaceMemberRoleBody {
  role: WorkspaceRole;
}
