"use client";

import { useRouter } from "next/navigation";
import { Button } from "@/components/common/Button";
import { formatDate } from "@/lib/format";
import { priorityLabel } from "@/lib/taskLabels";
import type { Task } from "@/types/task";
import type { Member } from "@/types/member";

interface TaskListProps {
  tasks: Task[];
  members: Member[];
  onAddClick: () => void;
  /** 状態キー→表示名(日本語)。無い場合はキーをそのまま表示する。 */
  statusLabels?: Record<string, string>;
  /** 一括操作モード。true のとき各行に選択チェックボックスを表示する。 */
  selectable?: boolean;
  /** 選択中のタスク ID 集合(selectable のときのみ使用)。 */
  selectedIds?: number[];
  /** 行の選択トグル。 */
  onToggleSelect?: (taskId: number) => void;
  /** 全選択/全解除のトグル(引数は現在表示中の全 ID)。 */
  onToggleSelectAll?: (taskIds: number[]) => void;
}

export function TaskList({
  tasks,
  members,
  onAddClick,
  statusLabels = {},
  selectable = false,
  selectedIds = [],
  onToggleSelect,
  onToggleSelectAll,
}: TaskListProps) {
  const router = useRouter();
  const selected = new Set(selectedIds);
  const allSelected = tasks.length > 0 && tasks.every((t) => selected.has(t.id));

  if (tasks.length === 0) {
    return (
      <div className="text-sm text-gray-500">
        <p className="mb-2">タスクがありません。</p>
        <Button type="button" variant="secondary" onClick={onAddClick}>
          タスクを追加
        </Button>
      </div>
    );
  }

  function assigneeName(assigneeId: number | null) {
    if (!assigneeId) return "-";
    return (
      members.find((member) => member.userId === assigneeId)?.name ?? "-"
    );
  }

  // サブタスクの「何の子か」を表示するため、同一プロジェクトのタスクから親タイトルを引く
  const titleById = new Map(tasks.map((t) => [t.id, t.title]));

  return (
    <table className="w-full border-collapse overflow-hidden rounded-lg bg-white text-sm shadow-sm">
      <thead>
        <tr className="border-b border-gray-200 text-left text-gray-500">
          {selectable && (
            <th className="w-10 px-4 py-2">
              <input
                type="checkbox"
                aria-label="全て選択"
                checked={allSelected}
                onChange={() => onToggleSelectAll?.(tasks.map((t) => t.id))}
              />
            </th>
          )}
          <th className="px-4 py-2 font-medium">タスク名</th>
          <th className="px-4 py-2 font-medium">担当者</th>
          <th className="px-4 py-2 font-medium">状態</th>
          <th className="px-4 py-2 font-medium">優先度</th>
          <th className="px-4 py-2 font-medium">期限</th>
        </tr>
      </thead>
      <tbody>
        {tasks.map((task) => (
          <tr
            key={task.id}
            onClick={() => router.push(`/tasks/${task.id}`)}
            className={`cursor-pointer border-b border-gray-100 last:border-0 hover:bg-gray-50 ${
              selectable && selected.has(task.id) ? "bg-blue-50" : ""
            }`}
          >
            {selectable && (
              <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                <input
                  type="checkbox"
                  aria-label={`${task.title} を選択`}
                  checked={selected.has(task.id)}
                  onChange={() => onToggleSelect?.(task.id)}
                />
              </td>
            )}
            {/* サブタスクの左アクセント線はセルに付ける(border-collapse では tr の左罫線が描画されないため) */}
            <td
              className={`px-4 py-3 text-gray-900 ${
                task.parentTaskId !== null ? "border-l-4 border-l-indigo-400" : ""
              }`}
            >
              {task.parentTaskId !== null && (
                <span className="mb-0.5 block truncate text-[11px] text-indigo-600">
                  ↳ {titleById.get(task.parentTaskId) ?? "親タスク"} のサブタスク
                </span>
              )}
              <span className="flex items-center gap-1.5">
                <span className="truncate">{task.title}</span>
                {task.subtaskProgress.total > 0 && (
                  <span className="shrink-0 rounded bg-indigo-50 px-1.5 py-0.5 text-[10px] font-normal text-indigo-700">
                    サブタスク {task.subtaskProgress.done}/{task.subtaskProgress.total}
                  </span>
                )}
              </span>
            </td>
            <td className="px-4 py-3 text-gray-600">
              {assigneeName(task.assigneeId)}
            </td>
            <td className="px-4 py-3 text-gray-600">{statusLabels[task.status] ?? task.status}</td>
            <td className="px-4 py-3 text-gray-600">{priorityLabel(task.priority)}</td>
            <td className="px-4 py-3 text-gray-600">
              {formatDate(task.dueDate)}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
