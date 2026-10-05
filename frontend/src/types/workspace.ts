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
