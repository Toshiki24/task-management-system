"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/common/Button";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import type { Watchers } from "@/types/task";

interface TaskWatchersProps {
  taskId: string;
}

export function TaskWatchers({ taskId }: TaskWatchersProps) {
  const [data, setData] = useState<Watchers | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  useEffect(() => {
    apiFetch<Watchers>(`/tasks/${taskId}/watchers`)
      .then(setData)
      .catch(() => setError("ウォッチャーの取得に失敗しました。"));
  }, [taskId]);

  async function toggle() {
    if (!data) return;
    setIsBusy(true);
    setError(null);
    try {
      const next = await apiFetch<Watchers>(`/tasks/${taskId}/watch`, {
        method: data.watching ? "DELETE" : "POST",
      });
      setData(next);
    } catch (err) {
      setError(formatApiErrorMessage(err, "ウォッチの更新に失敗しました。"));
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <div>
      <div className="mb-2 flex items-center justify-between">
        <h2 className="text-base font-bold text-gray-900">ウォッチャー</h2>
        {data && (
          <Button type="button" variant="secondary" onClick={toggle} disabled={isBusy} aria-pressed={data.watching}>
            {data.watching ? "フォロー中" : "フォロー"}
          </Button>
        )}
      </div>

      {error && <p className="mb-2 text-sm text-red-600">{error}</p>}

      {data && data.watchers.length > 0 ? (
        <ul className="flex flex-wrap gap-2">
          {data.watchers.map((watcher) => (
            <li
              key={watcher.userId}
              className="rounded-full bg-gray-100 px-2.5 py-0.5 text-xs text-gray-700"
            >
              {watcher.name}
            </li>
          ))}
        </ul>
      ) : (
        data && <p className="text-sm text-gray-500">まだ誰もフォローしていません。</p>
      )}
    </div>
  );
}
