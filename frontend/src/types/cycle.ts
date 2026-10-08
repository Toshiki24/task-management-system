export type CycleStatus = "PLANNED" | "ACTIVE" | "CLOSED";

export interface CycleProgress {
  done: number;
  total: number;
  points: number;
}

export interface Cycle {
  id: number;
  projectId: number;
  name: string;
  startDate: string | null;
  endDate: string | null;
  status: CycleStatus;
  progress: CycleProgress;
}

export interface CycleRequestBody {
  name: string;
  startDate?: string | null;
  endDate?: string | null;
  status?: CycleStatus;
}

export const CYCLE_STATUS_LABELS: Record<CycleStatus, string> = {
  PLANNED: "計画中",
  ACTIVE: "進行中",
  CLOSED: "完了",
};
