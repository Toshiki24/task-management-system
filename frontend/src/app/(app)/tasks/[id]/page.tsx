"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { CommentList } from "@/components/task/CommentList";
import { TaskChecklist } from "@/components/task/TaskChecklist";
import { TaskDetail } from "@/components/task/TaskDetail";
import { TaskForm } from "@/components/task/TaskForm";
import { TaskSubtasks } from "@/components/task/TaskSubtasks";
import { ApiError, apiFetch, formatApiErrorMessage } from "@/lib/api";
import { statusLabelMap } from "@/lib/taskLabels";
import type { Member } from "@/types/member";
import type { Project } from "@/types/project";
import type { Task } from "@/types/task";
import type { WorkflowState } from "@/types/workflow";

export default function TaskDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const taskId = params.id;

  const [task, setTask] = useState<Task | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [states, setStates] = useState<WorkflowState[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  useEffect(() => {
    apiFetch<Task>(`/tasks/${taskId}`)
      .then(setTask)
      .catch((err) =>
        // 削除済みなどタスクが存在しない場合は、APIの404メッセージをそのまま表示する
        setLoadError(
          err instanceof ApiError && err.status === 404
            ? err.message
            : "タスク情報の取得に失敗しました。",
        ),
      );
  }, [taskId]);

  useEffect(() => {
    if (!task) return;
    apiFetch<Member[]>(`/projects/${task.projectId}/members`)
      .then(setMembers)
      .catch(() => {
        // 担当者名の表示に使うだけなので、取得できなくても詳細自体は表示する
      });

    // 状態の表示名(日本語)に使うワークフロー状態を所属ワークスペースから取得する
    apiFetch<Project>(`/projects/${task.projectId}`)
      .then((project) => apiFetch<WorkflowState[]>(`/workspaces/${project.workspaceId}/workflow-states`))
      .then(setStates)
      .catch(() => {
        // 取得できなければ状態はキー表示にフォールバックする
      });
  }, [task]);

  async function handleDelete() {
    setIsDeleting(true);
    try {
      await apiFetch<void>(`/tasks/${taskId}`, { method: "DELETE" });
      router.push(task ? `/projects/${task.projectId}/tasks` : "/projects");
    } catch (err) {
      setDeleteError(formatApiErrorMessage(err, "タスクの削除に失敗しました。"));
      setIsDeleteModalOpen(false);
      setIsDeleting(false);
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

        {!isEditing && <TaskDetail task={task} members={members} statusLabels={statusLabelMap(states)} />}

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
              estimatePoints: task.estimatePoints,
              labelIds: task.labels.map((label) => label.id),
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

      {/* サブタスクは親タスク(トップレベル)のみ表示する(1 階層のため) */}
      {task.parentTaskId === null && (
        <div className="rounded-lg bg-white p-6 shadow-sm">
          <TaskSubtasks
            projectId={task.projectId}
            parentTaskId={task.id}
            statusLabels={statusLabelMap(states)}
          />
        </div>
      )}

      <div className="rounded-lg bg-white p-6 shadow-sm">
        <TaskChecklist taskId={taskId} />
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
            disabled={isDeleting}
          >
            キャンセル
          </Button>
          <Button
            type="button"
            variant="danger"
            onClick={handleDelete}
            disabled={isDeleting}
          >
            {isDeleting ? "削除中..." : "削除"}
          </Button>
        </div>
      </Modal>
    </div>
  );
}
