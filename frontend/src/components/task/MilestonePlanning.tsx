"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { formatDate } from "@/lib/format";
import { MILESTONE_STATUS_LABELS, type Milestone } from "@/types/milestone";
import type { Task } from "@/types/task";

interface MilestonePlanningProps {
  projectId: string;
  tasks: Task[];
  statusLabels?: Record<string, string>;
  /** 割り当て変更後にタスク一覧を再取得する。 */
  onReloadTasks: () => Promise<void>;
}

export function MilestonePlanning({ projectId, tasks, statusLabels = {}, onReloadTasks }: MilestonePlanningProps) {
  const router = useRouter();
  const [milestones, setMilestones] = useState<Milestone[]>([]);
  const [name, setName] = useState("");
  const [dueDate, setDueDate] = useState("");
  const [error, setError] = useState<string | null>(null);

  const loadMilestones = useCallback(() => {
    apiFetch<Milestone[]>(`/projects/${projectId}/milestones`)
      .then(setMilestones)
      .catch(() => setError("マイルストーンの取得に失敗しました。"));
  }, [projectId]);

  useEffect(() => {
    loadMilestones();
  }, [loadMilestones]);

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!name.trim()) return;
    setError(null);
    try {
      await apiFetch(`/projects/${projectId}/milestones`, {
        method: "POST",
        body: JSON.stringify({ name: name.trim(), dueDate: dueDate || null }),
      });
      setName("");
      setDueDate("");
      loadMilestones();
    } catch (err) {
      setError(formatApiErrorMessage(err, "マイルストーンの作成に失敗しました。"));
    }
  }

  async function handleDelete(milestoneId: number) {
    setError(null);
    try {
      await apiFetch(`/milestones/${milestoneId}`, { method: "DELETE" });
      loadMilestones();
      await onReloadTasks();
    } catch (err) {
      setError(formatApiErrorMessage(err, "マイルストーンの削除に失敗しました。"));
    }
  }

  async function assign(taskId: number, milestoneId: number | null) {
    setError(null);
    try {
      await apiFetch(`/projects/${projectId}/tasks/${taskId}/milestone`, {
        method: "PATCH",
        body: JSON.stringify({ milestoneId }),
      });
      await onReloadTasks();
      loadMilestones();
    } catch (err) {
      setError(formatApiErrorMessage(err, "マイルストーンの割り当てに失敗しました。"));
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
          aria-label={`${task.title} のマイルストーン`}
          value={task.milestoneId ?? ""}
          onChange={(e) => assign(task.id, e.target.value === "" ? null : Number(e.target.value))}
          className="shrink-0 rounded-md border border-gray-300 px-2 py-1 text-xs text-gray-900"
        >
          <option value="">未割り当て</option>
          {milestones.map((m) => (
            <option key={m.id} value={m.id}>
              {m.name}
            </option>
          ))}
        </select>
      </li>
    );
  }

  const unassigned = tasks.filter((t) => t.milestoneId === null);

  return (
    <div className="space-y-4">
      {error && <p className="text-sm text-red-600">{error}</p>}

      <form onSubmit={handleCreate} className="flex items-end gap-2">
        <div className="w-64">
          <Input id="newMilestoneName" label="マイルストーンを追加" value={name} onChange={(e) => setName(e.target.value)} placeholder="例: v1.0" />
        </div>
        <div className="w-44">
          <Input id="newMilestoneDue" type="date" label="期日(任意)" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
        </div>
        <Button type="submit" variant="secondary" disabled={!name.trim()}>
          作成
        </Button>
      </form>

      <div className="grid gap-3 md:grid-cols-2">
        {/* 未割り当て */}
        <section className="rounded-lg border border-gray-200 bg-white">
          <h3 className="border-b border-gray-100 px-3 py-2 text-sm font-semibold text-gray-700">
            未割り当て <span className="text-xs font-normal text-gray-500">({unassigned.length})</span>
          </h3>
          <ul className="divide-y divide-gray-100">
            {unassigned.length === 0 ? (
              <li className="px-3 py-3 text-xs text-gray-400">タスクはありません。</li>
            ) : (
              unassigned.map(taskRow)
            )}
          </ul>
        </section>

        {/* 各マイルストーン */}
        {milestones.map((milestone) => {
          const milestoneTasks = tasks.filter((t) => t.milestoneId === milestone.id);
          const ratio = milestone.progress.total === 0 ? 0 : Math.round((milestone.progress.done / milestone.progress.total) * 100);
          return (
            <section key={milestone.id} className="rounded-lg border border-gray-200 bg-white" aria-label={`マイルストーン: ${milestone.name}`}>
              <div className="border-b border-gray-100 px-3 py-2">
                <div className="flex items-center justify-between">
                  <h3 className="text-sm font-semibold text-gray-700">
                    {milestone.name}
                    <span className="ml-2 rounded bg-gray-100 px-1.5 py-0.5 text-[10px] font-normal text-gray-600">
                      {MILESTONE_STATUS_LABELS[milestone.status]}
                    </span>
                  </h3>
                  <div className="flex items-center gap-2 text-xs text-gray-500">
                    {milestone.dueDate && <span>期日 {formatDate(milestone.dueDate)}</span>}
                    <button
                      type="button"
                      onClick={() => handleDelete(milestone.id)}
                      className="text-red-600 hover:underline"
                      aria-label={`${milestone.name} を削除`}
                    >
                      削除
                    </button>
                  </div>
                </div>
                {/* 進捗バー */}
                <div className="mt-2 flex items-center gap-2">
                  <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-gray-100">
                    <div className="h-full rounded-full bg-blue-500" style={{ width: `${ratio}%` }} />
                  </div>
                  <span className="shrink-0 text-xs text-gray-500">
                    {milestone.progress.done}/{milestone.progress.total}・{milestone.progress.points}pt
                  </span>
                </div>
              </div>
              <ul className="divide-y divide-gray-100">
                {milestoneTasks.length === 0 ? (
                  <li className="px-3 py-3 text-xs text-gray-400">タスクはありません。</li>
                ) : (
                  milestoneTasks.map(taskRow)
                )}
              </ul>
            </section>
          );
        })}
      </div>
    </div>
  );
}
