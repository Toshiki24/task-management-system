import type { Label } from "@/types/label";

export interface MyTask {
  id: number;
  projectId: number;
  projectName: string;
  workspaceId: number;
  workspaceName: string;
  title: string;
  status: string;
  priority: string;
  dueDate: string | null;
  estimatePoints: number | null;
  labels: Label[];
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}
