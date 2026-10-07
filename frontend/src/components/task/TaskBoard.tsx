"use client";

import { useState, type DragEvent } from "react";
import { useRouter } from "next/navigation";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { priorityLabel } from "@/lib/taskLabels";
import type { Member } from "@/types/member";
import type { MoveTaskBody, Task } from "@/types/task";
import type { WorkflowState } from "@/types/workflow";

interface TaskBoardProps {
  projectId: string;
  tasks: Task[];
  states: WorkflowState[];
  members: Member[];
  /** 移動後に最新タスクへ更新する(楽観的更新に失敗したときの復帰にも使う) */
  onChanged: (tasks: Task[]) => void;
}

export function TaskBoard({ projectId, tasks, states, members, onChanged }: TaskBoardProps) {
  const router = useRouter();
  const [dragId, setDragId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  function assigneeName(assigneeId: number | null) {
    if (!assigneeId) return "-";
    return members.find((m) => m.userId === assigneeId)?.name ?? "-";
  }

  // サブタスクの「何の子か」を表示するため、同一プロジェクトのタスクから親タイトルを引く
  const titleById = new Map(tasks.map((t) => [t.id, t.title]));

  function columnTasks(stateKey: string): Task[] {
    return tasks
      .filter((t) => t.status === stateKey)
      .sort((a, b) => a.boardPosition - b.boardPosition || a.id - b.id);
  }

  async function move(taskId: number, toStatus: string, beforeTaskId: number | null) {
    const moving = tasks.find((t) => t.id === taskId);
    if (!moving) return;
    // 同じ位置へのドロップ(自分自身の前)は何もしない
    if (moving.status === toStatus && beforeTaskId === taskId) return;

    setError(null);
    const previous = tasks;
    // 楽観的更新: 対象の status を即時反映(並び順はサーバ確定値で後追い更新)
    onChanged(tasks.map((t) => (t.id === taskId ? { ...t, status: toStatus } : t)));

    try {
      const body: MoveTaskBody = { toStatus, beforeTaskId };
      await apiFetch<Task>(`/projects/${projectId}/tasks/${taskId}/move`, {
        method: "PATCH",
        body: JSON.stringify(body),
      });
      // サーバ確定の board_position を含む最新一覧で置き換える
      onChanged(await apiFetch<Task[]>(`/projects/${projectId}/tasks`));
    } catch (err) {
      setError(formatApiErrorMessage(err, "カードの移動に失敗しました。"));
      onChanged(previous);
    }
  }

  function onDropToCard(event: DragEvent, toStatus: string, beforeTaskId: number) {
    event.preventDefault();
    event.stopPropagation();
    if (dragId !== null) void move(dragId, toStatus, beforeTaskId);
    setDragId(null);
  }

  function onDropToColumn(event: DragEvent, toStatus: string) {
    event.preventDefault();
    if (dragId !== null) void move(dragId, toStatus, null);
    setDragId(null);
  }

  return (
    <div>
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      <div className="flex gap-4 overflow-x-auto pb-2">
        {states.map((state) => {
          const items = columnTasks(state.key);
          return (
            <section
              key={state.id}
              aria-label={`列: ${state.name}`}
              className="flex w-72 shrink-0 flex-col rounded-lg bg-gray-50 p-3"
              onDragOver={(e) => e.preventDefault()}
              onDrop={(e) => onDropToColumn(e, state.key)}
            >
              <h3 className="mb-2 flex items-center justify-between text-sm font-semibold text-gray-700">
                <span>{state.name}</span>
                <span className="rounded bg-gray-200 px-1.5 text-xs text-gray-600">{items.length}</span>
              </h3>
              <div className="flex flex-col gap-2">
                {items.map((task) => (
                  <article
                    key={task.id}
                    draggable
                    onDragStart={() => setDragId(task.id)}
                    onDragEnd={() => setDragId(null)}
                    onDragOver={(e) => e.preventDefault()}
                    onDrop={(e) => onDropToCard(e, state.key, task.id)}
                    onClick={() => router.push(`/tasks/${task.id}`)}
                    className={`cursor-pointer rounded-md border border-gray-200 bg-white p-3 text-sm shadow-sm hover:border-blue-300 ${
                      task.parentTaskId !== null ? "border-l-4 border-l-indigo-400" : ""
                    }`}
                  >
                    {task.parentTaskId !== null && (
                      <p className="mb-0.5 truncate text-[11px] text-indigo-600">
                        ↳ {titleById.get(task.parentTaskId) ?? "親タスク"} のサブタスク
                      </p>
                    )}
                    <p className="mb-1 flex items-center gap-1.5 font-medium text-gray-900">
                      <span className="truncate">{task.title}</span>
                      {task.subtaskProgress.total > 0 && (
                        <span className="shrink-0 rounded bg-indigo-50 px-1.5 py-0.5 text-[10px] font-normal text-indigo-700">
                          サブタスク {task.subtaskProgress.done}/{task.subtaskProgress.total}
                        </span>
                      )}
                    </p>
                    {task.labels.length > 0 && (
                      <p className="mb-1 flex flex-wrap gap-1">
                        {task.labels.map((label) => (
                          <span
                            key={label.id}
                            className="rounded-full px-1.5 py-0.5 text-[10px] text-white"
                            style={{ backgroundColor: label.color ?? "#6b7280" }}
                          >
                            {label.name}
                          </span>
                        ))}
                      </p>
                    )}
                    <p className="flex items-center justify-between text-xs text-gray-500">
                      <span>{assigneeName(task.assigneeId)}</span>
                      <span>
                        {task.estimatePoints != null && <span className="mr-2">{task.estimatePoints}pt</span>}
                        優先度: {priorityLabel(task.priority)}
                      </span>
                    </p>
                  </article>
                ))}
                {items.length === 0 && (
                  <p className="rounded-md border border-dashed border-gray-200 p-3 text-center text-xs text-gray-400">
                    ここにドロップ
                  </p>
                )}
              </div>
            </section>
          );
        })}
      </div>
    </div>
  );
}
