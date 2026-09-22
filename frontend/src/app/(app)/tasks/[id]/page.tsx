"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { CommentList } from "@/components/task/CommentList";
import { TaskDetail } from "@/components/task/TaskDetail";
import { TaskForm } from "@/components/task/TaskForm";
import { apiFetch } from "@/lib/api";
import type { Member } from "@/types/member";
import type { Task } from "@/types/task";

export default function TaskDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const taskId = params.id;

  const [task, setTask] = useState<Task | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<Task>(`/tasks/${taskId}`)
      .then(setTask)
      .catch(() => setLoadError("タスク情報の取得に失敗しました。"));
  }, [taskId]);

  useEffect(() => {
    if (!task) return;
    apiFetch<Member[]>(`/projects/${task.projectId}/members`)
      .then(setMembers)
      .catch(() => {
        // 担当者名の表示に使うだけなので、取得できなくても詳細自体は表示する
      });
  }, [task]);

  async function handleDelete() {
    try {
      await apiFetch<void>(`/tasks/${taskId}`, { method: "DELETE" });
      router.push(task ? `/projects/${task.projectId}/tasks` : "/projects");
    } catch {
      setDeleteError("タスクの削除に失敗しました。");
      setIsDeleteModalOpen(false);
    }
  }

  if (loadError) {
    return <ErrorMessage message={loadError} />;
  }

  if (!task) {
    return <Loading />;
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="rounded-lg bg-white p-6 shadow-sm">
        <div className="mb-4 flex items-center justify-between">
          <h1 className="text-lg font-bold text-gray-900">タスク詳細</h1>
          {!isEditing && (
            <div className="flex gap-2">
              <Button
                type="button"
                variant="secondary"
                onClick={() => setIsEditing(true)}
              >
                編集
              </Button>
              <Button
                type="button"
                variant="danger"
                onClick={() => setIsDeleteModalOpen(true)}
              >
                削除
              </Button>
            </div>
          )}
        </div>

        {deleteError && <ErrorMessage message={deleteError} />}

        {!isEditing && <TaskDetail task={task} members={members} />}

        {isEditing && (
          <TaskForm
            projectId={task.projectId}
            initialValue={{
              assigneeId: task.assigneeId,
              title: task.title,
              description: task.description,
              status: task.status,
              priority: task.priority,
              dueDate: task.dueDate,
            }}
            submitLabel="保存"
            submittingLabel="保存中..."
            onSubmit={async (value) => {
              const updated = await apiFetch<Task>(`/tasks/${taskId}`, {
                method: "PUT",
                body: JSON.stringify(value),
              });
              setTask(updated);
              setIsEditing(false);
            }}
            onCancel={() => setIsEditing(false)}
          />
        )}
      </div>

      <div className="rounded-lg bg-white p-6 shadow-sm">
        <h2 className="mb-4 text-base font-bold text-gray-900">コメント</h2>
        <CommentList taskId={taskId} />
      </div>

      <Modal
        isOpen={isDeleteModalOpen}
        title="タスク削除"
        onClose={() => setIsDeleteModalOpen(false)}
      >
        <p>
          このタスクを削除しますか？
          <br />
          タスクに紐付くコメントも削除されます。
        </p>
        <div className="mt-6 flex justify-end gap-3">
          <Button
            type="button"
            variant="secondary"
            onClick={() => setIsDeleteModalOpen(false)}
          >
            キャンセル
          </Button>
          <Button type="button" variant="danger" onClick={handleDelete}>
            削除
          </Button>
        </div>
      </Modal>
    </div>
  );
}
