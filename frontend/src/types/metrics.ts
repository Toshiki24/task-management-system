export interface StatusCount {
  key: string;
  name: string;
  category: string;
  count: number;
}

export interface AssigneeLoad {
  assigneeId: number | null;
  name: string;
  openCount: number;
  estimatePoints: number;
}

export interface Metrics {
  total: number;
  doneCount: number;
  completionRate: number;
  overdueCount: number;
  statusCounts: StatusCount[];
  assigneeLoads: AssigneeLoad[];
}
