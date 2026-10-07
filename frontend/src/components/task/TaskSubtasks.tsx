"use client";

import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import type { Task } from "@/types/task";

interface TaskSubtasksProps {
  projectId: number;
  parentTaskId: number;
  /** 状態キー→表示名(日本語)。 */
  statusLabels?: Record<string, string>;
}

export function TaskSubtasks({ projectId, parentTaskId, statusLabels = {} }: TaskSubtasksProps) {
  const [subtasks, setSubtasks] = useState<Task[] | null>(null);
  const [title, setTitle] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<Task[]>(`/projects/${projectId}/tasks`)
      .then((tasks) => setSubtasks(tasks.filter((t) => t.parentTaskId === parentTaskId)))
      .catch(() => setError("サブタスクの取得に失敗しました。"));
  }, [projectId, parentTaskId]);

  async function reload() {
    const tasks = await apiFetch<Task[]>(`/projects/${projectId}/tasks`);
    setSubtasks(tasks.filter((t) => t.parentTaskId === parentTaskId));
  }

  async function handleAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!title.trim()) return;
    setError(null);
    try {
      await apiFetch(`/projects/${projectId}/tasks`, {
        method: "POST",
        body: JSON.stringify({ title: title.trim(), parentTaskId }),
      });
      setTitle("");
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "サブタスクの追加に失敗しました。"));
    }
  }

  return (
    <div>
      <h2 className="mb-2 text-base font-bold text-gray-900">サブタスク</h2>

      {error && <p className="mb-2 text-sm text-red-600">{error}</p>}

      {subtasks && subtasks.length > 0 && (
        <ul className="mb-3 divide-y divide-gray-100 rounded-md border border-gray-200">
          {subtasks.map((task) => (
            <li key={task.id}>
              <Link
                href={`/tasks/${task.id}`}
                className="flex items-center justify-between gap-2 px-3 py-2 text-sm hover:bg-gray-50"
              >
                <span className="truncate text-gray-900">{task.title}</span>
                <span className="shrink-0 text-xs text-gray-500">
                  {statusLabels[task.status] ?? task.status}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}

      <form onSubmit={handleAdd} className="flex items-end gap-2">
        <div className="flex-1">
          <Input
            id="newSubtaskTitle"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="サブタスクを追加"
          />
        </div>
        <Button type="submit" variant="secondary">
          追加
        </Button>
      </form>
    </div>
  );
}
