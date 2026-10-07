import type { WorkflowState } from "@/types/workflow";

/** 優先度の日本語表記。表示は全画面でこれに統一する。 */
export const PRIORITY_LABELS: Record<string, string> = {
  HIGH: "高",
  MEDIUM: "中",
  LOW: "低",
};

export function priorityLabel(priority: string): string {
  return PRIORITY_LABELS[priority] ?? priority;
}

/** ワークフロー状態の key→表示名(日本語)マップを作る。状態の表示名で統一するために使う。 */
export function statusLabelMap(states: WorkflowState[]): Record<string, string> {
  return Object.fromEntries(states.map((s) => [s.key, s.name]));
}
