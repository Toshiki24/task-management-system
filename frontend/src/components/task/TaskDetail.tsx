import { formatDate } from "@/lib/format";
import { priorityLabel } from "@/lib/taskLabels";
import type { Member } from "@/types/member";
import type { Task } from "@/types/task";

interface TaskDetailProps {
  task: Task;
  members: Member[];
  /** 状態キー→表示名(日本語)。無い場合はキーをそのまま表示する。 */
  statusLabels?: Record<string, string>;
}

export function TaskDetail({ task, members, statusLabels = {} }: TaskDetailProps) {
  const assigneeName =
    members.find((member) => member.userId === task.assigneeId)?.name ?? "-";

  return (
    <dl className="space-y-4 text-sm">
      <div>
        <dt className="text-gray-500">タイトル</dt>
        <dd className="mt-0.5 text-gray-900">{task.title}</dd>
      </div>
      <div className="flex gap-8">
        <div>
          <dt className="text-gray-500">ステータス</dt>
          <dd className="mt-0.5 text-gray-900">{statusLabels[task.status] ?? task.status}</dd>
        </div>
        <div>
          <dt className="text-gray-500">優先度</dt>
          <dd className="mt-0.5 text-gray-900">{priorityLabel(task.priority)}</dd>
        </div>
      </div>
      <div className="flex gap-8">
        <div>
          <dt className="text-gray-500">担当者</dt>
          <dd className="mt-0.5 text-gray-900">{assigneeName}</dd>
        </div>
        <div>
          <dt className="text-gray-500">期限</dt>
          <dd className="mt-0.5 text-gray-900">{formatDate(task.dueDate)}</dd>
        </div>
        <div>
          <dt className="text-gray-500">見積</dt>
          <dd className="mt-0.5 text-gray-900">{task.estimatePoints ?? "-"}</dd>
        </div>
      </div>
      <div>
        <dt className="text-gray-500">ラベル</dt>
        <dd className="mt-0.5">
          {task.labels.length === 0 ? (
            <span className="text-gray-900">-</span>
          ) : (
            <span className="flex flex-wrap gap-1.5">
              {task.labels.map((label) => (
                <span
                  key={label.id}
                  className="rounded-full px-2 py-0.5 text-xs text-white"
                  style={{ backgroundColor: label.color ?? "#6b7280" }}
                >
                  {label.name}
                </span>
              ))}
            </span>
          )}
        </dd>
      </div>
      <div>
        <dt className="text-gray-500">説明</dt>
        <dd className="mt-0.5 whitespace-pre-wrap text-gray-900">
          {task.description ?? "-"}
        </dd>
      </div>
    </dl>
  );
}
