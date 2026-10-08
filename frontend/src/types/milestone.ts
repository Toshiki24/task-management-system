export type MilestoneStatus = "OPEN" | "CLOSED";

export interface MilestoneProgress {
  done: number;
  total: number;
  points: number;
}

export interface Milestone {
  id: number;
  projectId: number;
  name: string;
  dueDate: string | null;
  status: MilestoneStatus;
  progress: MilestoneProgress;
}

export interface MilestoneRequestBody {
  name: string;
  dueDate?: string | null;
  status?: MilestoneStatus;
}

export const MILESTONE_STATUS_LABELS: Record<MilestoneStatus, string> = {
  OPEN: "進行中",
  CLOSED: "完了",
};
