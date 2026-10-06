// status はワークスペースのワークフロー(workflow_states.key)を指す動的な値。
// 既定は TODO/IN_PROGRESS/DONE だが、ワークスペースごとに追加・変更できる(M2)。
export type TaskStatus = string;
export type TaskPriority = "LOW" | "MEDIUM" | "HIGH";

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
}

export interface TaskRequestBody {
  assigneeId?: number | null;
  title: string;
  description?: string | null;
  status?: TaskStatus;
  priority?: TaskPriority;
  dueDate?: string | null;
}

export interface MoveTaskBody {
  toStatus: string;
  beforeTaskId: number | null;
}
