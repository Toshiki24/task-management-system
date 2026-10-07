// status はワークスペースのワークフロー(workflow_states.key)を指す動的な値。
// 既定は TODO/IN_PROGRESS/DONE だが、ワークスペースごとに追加・変更できる(M2)。
export type TaskStatus = string;
export type TaskPriority = "LOW" | "MEDIUM" | "HIGH";

import type { Label } from "@/types/label";

export interface Task {
  id: number;
  projectId: number;
  assigneeId: number | null;
  title: string;
  description: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  dueDate: string | null;
  boardPosition: number;
  estimatePoints: number | null;
  labels: Label[];
  parentTaskId: number | null;
  subtaskProgress: { done: number; total: number };
}

export interface TaskRequestBody {
  assigneeId?: number | null;
  title: string;
  description?: string | null;
  status?: TaskStatus;
  priority?: TaskPriority;
  dueDate?: string | null;
  estimatePoints?: number | null;
  labelIds?: number[];
  parentTaskId?: number | null;
}

/** 依存関係で結ばれた相手タスクの要約(依存辺 1 本 = 1 件)。 */
export interface DependencyLink {
  dependencyId: number;
  taskId: number;
  title: string;
  status: string;
  isClosed: boolean;
}

/** あるタスクの依存関係。blockedBy=このタスクを待たせているタスク、blocking=このタスクが待たせているタスク。 */
export interface TaskDependencies {
  blockedBy: DependencyLink[];
  blocking: DependencyLink[];
}

export interface ChecklistItem {
  id: number;
  taskId: number;
  content: string;
  isDone: boolean;
  position: number;
}

export interface MoveTaskBody {
  toStatus: string;
  beforeTaskId: number | null;
}

/** 複数タスクの一括更新(M2 §5.4)。指定した項目だけを全対象へ適用する。 */
export interface BulkUpdateBody {
  taskIds: number[];
  status?: string;
  setAssignee?: boolean;
  assigneeId?: number | null;
  addLabelIds?: number[];
  removeLabelIds?: number[];
}
