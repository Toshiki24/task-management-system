"use client";

import { useEffect, useState } from "react";
import { apiFetch } from "@/lib/api";
import { formatDateTime } from "@/lib/format";
import type { Activity } from "@/types/task";

interface TaskActivityProps {
  taskId: string;
  /** 状態キー→表示名(日本語)。MOVED の from/to 表示に使う。 */
  statusLabels?: Record<string, string>;
}

export function TaskActivity({ taskId, statusLabels = {} }: TaskActivityProps) {
  const [activities, setActivities] = useState<Activity[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<Activity[]>(`/tasks/${taskId}/activities`)
      .then(setActivities)
      .catch(() => setError("アクティビティの取得に失敗しました。"));
  }, [taskId]);

  function message(activity: Activity): string {
    const p = activity.payload ?? {};
    switch (activity.verb) {
      case "CREATED":
        return "がタスクを作成しました";
      case "UPDATED": {
        const fields = Array.isArray(p.fields) ? (p.fields as string[]) : [];
        return fields.length > 0 ? `が ${fields.join("・")} を変更しました` : "がタスクを更新しました";
      }
      case "MOVED": {
        const from = statusLabels[p.from as string] ?? (p.from as string) ?? "";
        const to = statusLabels[p.to as string] ?? (p.to as string) ?? "";
        return `が状態を「${from}」→「${to}」に変更しました`;
      }
      case "COMMENTED":
        return "がコメントしました";
      case "GIT_BRANCH_CREATED":
        return `がブランチ ${(p.ref as string) ?? ""} を作成しました`;
      case "GIT_PR_OPENED":
        return "が PR/MR を作成しました";
      case "GIT_PR_MERGED":
        return "が PR/MR をマージしました";
      case "GIT_PR_CLOSED":
        return "が PR/MR をクローズしました";
      case "GIT_COMMIT_LINKED":
        return "がコミットを紐づけました";
      default:
        return "が操作しました";
    }
  }

  return (
    <div>
      <h2 className="mb-2 text-base font-bold text-gray-900">アクティビティ</h2>

      {error && <p className="mb-2 text-sm text-red-600">{error}</p>}

      {activities && activities.length === 0 && (
        <p className="text-sm text-gray-500">まだアクティビティはありません。</p>
      )}

      {activities && activities.length > 0 && (
        <ul className="space-y-3">
          {activities.map((activity) => (
            <li key={activity.id} className="flex gap-3 text-sm">
              <span aria-hidden className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-gray-300" />
              <div className="min-w-0">
                <p className="text-gray-900">
                  <span className="font-medium">{activity.actorName}</span>
                  {message(activity)}
                  {activity.verb === "COMMENTED" && activity.payload?.excerpt ? (
                    <span className="text-gray-500">：{String(activity.payload.excerpt)}</span>
                  ) : null}
                </p>
                <p className="text-xs text-gray-400">{formatDateTime(activity.createdAt)}</p>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
