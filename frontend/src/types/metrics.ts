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

export interface ThroughputPoint {
  date: string;
  count: number;
}

export interface DevMetrics {
  days: number;
  avgCycleTimeHours: number | null;
  avgLeadTimeHours: number | null;
  completedInPeriod: number;
  throughput: ThroughputPoint[];
}

export interface ImportRowError {
  row: number;
  message: string;
}

export interface ImportResult {
  imported: number;
  failed: ImportRowError[];
}
