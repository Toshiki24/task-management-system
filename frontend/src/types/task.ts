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
  cycleId: number | null;
  milestoneId: number | null;
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

/** アクティビティ(タスクの活動履歴。M3 §5)。payload は種別ごとの付加情報。 */
export type ActivityVerb =
  | "CREATED"
  | "UPDATED"
  | "MOVED"
  | "COMMENTED"
  | "GIT_BRANCH_CREATED"
  | "GIT_PR_OPENED"
  | "GIT_PR_MERGED"
  | "GIT_PR_CLOSED"
  | "GIT_COMMIT_LINKED";

export interface Activity {
  id: number;
  taskId: number | null;
  actorUserId: number;
  actorName: string;
  verb: ActivityVerb;
  payload: Record<string, unknown> | null;
  createdAt: string;
}

/** ウォッチャー(フォロー。M3 §4)。 */
export interface Watcher {
  userId: number;
  name: string;
}

export interface Watchers {
  watchers: Watcher[];
  watching: boolean;
}

/** コマンドパレットの横断検索で返るタスク要約(M2 §5.5)。 */
export interface TaskSearchResult {
  id: number;
  projectId: number;
  projectName: string;
  title: string;
  status: string;
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
