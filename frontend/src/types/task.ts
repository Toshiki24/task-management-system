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
