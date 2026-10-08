"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { CYCLE_STATUS_LABELS, type Cycle } from "@/types/cycle";
import type { Task } from "@/types/task";

interface CyclePlanningProps {
  projectId: string;
  tasks: Task[];
  statusLabels?: Record<string, string>;
  /** 割り当て変更後にタスク一覧を再取得する。 */
  onReloadTasks: () => Promise<void>;
}

export function CyclePlanning({ projectId, tasks, statusLabels = {}, onReloadTasks }: CyclePlanningProps) {
  const router = useRouter();
  const [cycles, setCycles] = useState<Cycle[]>([]);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);

  const loadCycles = useCallback(() => {
    apiFetch<Cycle[]>(`/projects/${projectId}/cycles`)
      .then(setCycles)
      .catch(() => setError("サイクルの取得に失敗しました。"));
  }, [projectId]);

  useEffect(() => {
    loadCycles();
  }, [loadCycles]);

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!name.trim()) return;
    setError(null);
    try {
      await apiFetch(`/projects/${projectId}/cycles`, {
        method: "POST",
        body: JSON.stringify({ name: name.trim() }),
      });
      setName("");
      loadCycles();
    } catch (err) {
      setError(formatApiErrorMessage(err, "サイクルの作成に失敗しました。"));
    }
  }

  async function handleDelete(cycleId: number) {
    setError(null);
    try {
      await apiFetch(`/cycles/${cycleId}`, { method: "DELETE" });
      loadCycles();
      await onReloadTasks();
    } catch (err) {
      setError(formatApiErrorMessage(err, "サイクルの削除に失敗しました。"));
    }
  }

  async function assign(taskId: number, cycleId: number | null) {
    setError(null);
    try {
      await apiFetch(`/projects/${projectId}/tasks/${taskId}/cycle`, {
        method: "PATCH",
        body: JSON.stringify({ cycleId }),
      });
      await onReloadTasks();
      loadCycles();
    } catch (err) {
      setError(formatApiErrorMessage(err, "サイクルの割り当てに失敗しました。"));
    }
  }

  function taskRow(task: Task) {
    return (
      <li key={task.id} className="flex items-center justify-between gap-2 px-3 py-2 text-sm">
        <button
          type="button"
          onClick={() => router.push(`/tasks/${task.id}`)}
          className="truncate text-left text-gray-900 hover:underline"
        >
          {task.title}
          <span className="ml-2 text-xs text-gray-500">{statusLabels[task.status] ?? task.status}</span>
        </button>
        <select
          aria-label={`${task.title} のサイクル`}
          value={task.cycleId ?? ""}
          onChange={(e) => assign(task.id, e.target.value === "" ? null : Number(e.target.value))}
          className="shrink-0 rounded-md border border-gray-300 px-2 py-1 text-xs text-gray-900"
        >
          <option value="">バックログ</option>
          {cycles.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </li>
    );
  }

  const backlog = tasks.filter((t) => t.cycleId === null);

  return (
    <div className="space-y-4">
      {error && <p className="text-sm text-red-600">{error}</p>}

      <form onSubmit={handleCreate} className="flex items-end gap-2">
        <div className="w-64">
          <Input id="newCycleName" label="サイクルを追加" value={name} onChange={(e) => setName(e.target.value)} placeholder="例: Sprint 1" />
        </div>
        <Button type="submit" variant="secondary" disabled={!name.trim()}>
          作成
        </Button>
      </form>

      <div className="grid gap-3 md:grid-cols-2">
        {/* バックログ */}
        <section className="rounded-lg border border-gray-200 bg-white">
          <h3 className="border-b border-gray-100 px-3 py-2 text-sm font-semibold text-gray-700">
            バックログ <span className="text-xs font-normal text-gray-500">({backlog.length})</span>
          </h3>
          <ul className="divide-y divide-gray-100">
            {backlog.length === 0 ? (
              <li className="px-3 py-3 text-xs text-gray-400">タスクはありません。</li>
            ) : (
              backlog.map(taskRow)
            )}
          </ul>
        </section>

        {/* 各サイクル */}
        {cycles.map((cycle) => {
          const cycleTasks = tasks.filter((t) => t.cycleId === cycle.id);
          return (
            <section key={cycle.id} className="rounded-lg border border-gray-200 bg-white" aria-label={`サイクル: ${cycle.name}`}>
              <div className="flex items-center justify-between border-b border-gray-100 px-3 py-2">
                <h3 className="text-sm font-semibold text-gray-700">
                  {cycle.name}
                  <span className="ml-2 rounded bg-gray-100 px-1.5 py-0.5 text-[10px] font-normal text-gray-600">
                    {CYCLE_STATUS_LABELS[cycle.status]}
                  </span>
                </h3>
                <div className="flex items-center gap-2 text-xs text-gray-500">
                  <span>
                    {cycle.progress.done}/{cycle.progress.total}・{cycle.progress.points}pt
                  </span>
                  <button
                    type="button"
                    onClick={() => handleDelete(cycle.id)}
                    className="text-red-600 hover:underline"
                    aria-label={`${cycle.name} を削除`}
                  >
                    削除
                  </button>
                </div>
              </div>
              <ul className="divide-y divide-gray-100">
                {cycleTasks.length === 0 ? (
                  <li className="px-3 py-3 text-xs text-gray-400">タスクはありません。</li>
                ) : (
                  cycleTasks.map(taskRow)
                )}
              </ul>
            </section>
          );
        })}
      </div>
    </div>
  );
}
