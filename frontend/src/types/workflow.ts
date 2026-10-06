export type WorkflowStateCategory =
  | "BACKLOG"
  | "TODO"
  | "IN_PROGRESS"
  | "DONE"
  | "CANCELLED";

export interface WorkflowState {
  id: number;
  workspaceId: number;
  key: string;
  name: string;
  category: WorkflowStateCategory;
  position: number;
  isDefault: boolean;
  color: string | null;
}

export interface CreateWorkflowStateBody {
  key: string;
  name: string;
  category: WorkflowStateCategory;
  color?: string | null;
}

export interface UpdateWorkflowStateBody {
  name: string;
  category: WorkflowStateCategory;
  position?: number;
  color?: string | null;
}

export const WORKFLOW_CATEGORY_LABELS: Record<WorkflowStateCategory, string> = {
  BACKLOG: "バックログ",
  TODO: "未着手",
  IN_PROGRESS: "進行中",
  DONE: "完了",
  CANCELLED: "中止",
};
