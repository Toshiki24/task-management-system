"use client";

import { useRouter } from "next/navigation";
import type { Task } from "@/types/task";
import type { Member } from "@/types/member";

interface TaskListProps {
  tasks: Task[];
  members: Member[];
}

export function TaskList({ tasks, members }: TaskListProps) {
  const router = useRouter();

  if (tasks.length === 0) {
    return <p className="text-sm text-gray-500">タスクがありません。</p>;
  }

  function assigneeName(assigneeId: number | null) {
    if (!assigneeId) return "-";
    return (
      members.find((member) => member.userId === assigneeId)?.name ?? "-"
    );
  }

  return (
    <table className="w-full border-collapse overflow-hidden rounded-lg bg-white text-sm shadow-sm">
      <thead>
        <tr className="border-b border-gray-200 text-left text-gray-500">
          <th className="px-4 py-2 font-medium">タスク名</th>
          <th className="px-4 py-2 font-medium">担当者</th>
          <th className="px-4 py-2 font-medium">状態</th>
          <th className="px-4 py-2 font-medium">優先度</th>
        </tr>
      </thead>
      <tbody>
        {tasks.map((task) => (
          <tr
            key={task.id}
            onClick={() => router.push(`/tasks/${task.id}`)}
            className="cursor-pointer border-b border-gray-100 last:border-0 hover:bg-gray-50"
          >
            <td className="px-4 py-3 text-gray-900">{task.title}</td>
            <td className="px-4 py-3 text-gray-600">
              {assigneeName(task.assigneeId)}
            </td>
            <td className="px-4 py-3 text-gray-600">{task.status}</td>
            <td className="px-4 py-3 text-gray-600">{task.priority}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
