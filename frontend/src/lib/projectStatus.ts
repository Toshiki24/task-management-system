import type { ProjectStatus } from "@/types/project";

/** プロジェクト状態の日本語表記。表示は全画面でこれに統一する。 */
export const PROJECT_STATUS_LABELS: Record<ProjectStatus, string> = {
  ACTIVE: "進行中",
  COMPLETED: "完了",
  ARCHIVED: "アーカイブ",
};

export function projectStatusLabel(status: string): string {
  return PROJECT_STATUS_LABELS[status as ProjectStatus] ?? status;
}
