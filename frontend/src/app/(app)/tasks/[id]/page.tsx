"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { CommentList } from "@/components/task/CommentList";
import { TaskActivity } from "@/components/task/TaskActivity";
import { TaskChecklist } from "@/components/task/TaskChecklist";
import { TaskDependencies } from "@/components/task/TaskDependencies";
import { TaskDetail } from "@/components/task/TaskDetail";
import { TaskForm } from "@/components/task/TaskForm";
import { TaskSubtasks } from "@/components/task/TaskSubtasks";
import { ApiError, apiFetch, formatApiErrorMessage } from "@/lib/api";
import { statusLabelMap } from "@/lib/taskLabels";
import type { Member } from "@/types/member";
import type { Project } from "@/types/project";
import type { DependencyLink, Task, TaskDependencies as Deps, TaskRequestBody } from "@/types/task";
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
  // 完了(DONE)にしようとしたが未完了ブロッカーが残っている場合の確認状態
  const [confirmComplete, setConfirmComplete] = useState<
    { value: TaskRequestBody; openBlockers: DependencyLink[] } | null
  >(null);
  const [isCompleting, setIsCompleting] = useState(false);
  const [completeError, setCompleteError] = useState<string | null>(null);
  // ブロッカーを一括完了した後、依存関係セクションを再読み込みするためのキー
  const [depsRefreshKey, setDepsRefreshKey] = useState(0);
  // 編集・完了操作のあとにアクティビティを再読み込みするためのキー
  const [activityRefreshKey, setActivityRefreshKey] = useState(0);

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

  /** その状態キーが「完了」カテゴリ(DONE)かどうか。 */
  function isDoneStatus(statusKey: string | null | undefined): boolean {
    return !!statusKey && states.some((s) => s.key === statusKey && s.category === "DONE");
  }

  async function saveTask(value: TaskRequestBody) {
    const updated = await apiFetch<Task>(`/tasks/${taskId}`, {
      method: "PUT",
      body: JSON.stringify(value),
    });
    setTask(updated);
    setIsEditing(false);
    setActivityRefreshKey((k) => k + 1);
  }

  /**
   * 編集保存のハンドラ。完了(DONE)にしようとしていて未完了ブロッカーが残っている場合は、
   * いったん確認モーダルを開き、ユーザーの確認を待ってから保存する(ブロッカーがあっても完了は可能)。
   */
  async function handleFormSubmit(value: TaskRequestBody) {
    if (task && isDoneStatus(value.status) && !isDoneStatus(task.status)) {
      const deps = await apiFetch<Deps>(`/tasks/${taskId}/dependencies`);
      const openBlockers = deps.blockedBy.filter((d) => !d.isClosed);
      if (openBlockers.length > 0) {
        setCompleteError(null);
        setConfirmComplete({ value, openBlockers });
        return; // モーダルの確認を待つ(編集フォームは開いたまま)
      }
    }
    await saveTask(value);
  }

  /** 確認モーダルで「全て完了にする」を選んだとき: ブロッカーを順に完了にしてから本タスクを保存する。 */
  async function handleConfirmComplete() {
    if (!confirmComplete || !task) return;
    const doneKey = states.find((s) => s.category === "DONE")?.key;
    setIsCompleting(true);
    setCompleteError(null);
    try {
      if (doneKey) {
        // move は {toStatus} だけで状態変更できる(全フィールドを送る PUT を避ける)
        for (const blocker of confirmComplete.openBlockers) {
          await apiFetch(`/projects/${task.projectId}/tasks/${blocker.taskId}/move`, {
            method: "PATCH",
            body: JSON.stringify({ toStatus: doneKey, beforeTaskId: null }),
          });
        }
      }
      await saveTask(confirmComplete.value);
      setConfirmComplete(null);
      setDepsRefreshKey((k) => k + 1); // 依存関係セクションを最新化する
    } catch (err) {
      setCompleteError(formatApiErrorMessage(err, "ブロッカーの完了に失敗しました。"));
    } finally {
      setIsCompleting(false);
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
            onSubmit={handleFormSubmit}
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
        <TaskDependencies
          key={depsRefreshKey}
          taskId={taskId}
          projectId={task.projectId}
          statusLabels={statusLabelMap(states)}
        />
      </div>

      <div className="rounded-lg bg-white p-6 shadow-sm">
        <TaskChecklist taskId={taskId} />
      </div>

      <div className="rounded-lg bg-white p-6 shadow-sm">
        <h2 className="mb-4 text-base font-bold text-gray-900">コメント</h2>
        <CommentList taskId={taskId} projectId={task.projectId} />
      </div>

      <div className="rounded-lg bg-white p-6 shadow-sm">
        <TaskActivity
          key={activityRefreshKey}
          taskId={taskId}
          statusLabels={statusLabelMap(states)}
        />
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

      <Modal
        isOpen={confirmComplete !== null}
        title="未完了のブロッカーがあります"
        onClose={() => (isCompleting ? undefined : setConfirmComplete(null))}
      >
        <p className="text-sm text-gray-700">
          このタスクには、まだ完了していない次のブロッカーがあります。
          <br />
          これらも全て完了にして、このタスクを完了にしますか？
        </p>
        <ul className="mt-3 max-h-48 list-disc space-y-1 overflow-y-auto rounded-md border border-gray-200 bg-gray-50 px-6 py-3 text-sm text-gray-800">
          {confirmComplete?.openBlockers.map((blocker) => (
            <li key={blocker.dependencyId}>
              {blocker.title}
              <span className="ml-1 text-xs text-gray-500">
                ({statusLabelMap(states)[blocker.status] ?? blocker.status})
              </span>
            </li>
          ))}
        </ul>

        {completeError && (
          <div className="mt-3">
            <ErrorMessage message={completeError} />
          </div>
        )}

        <div className="mt-6 flex justify-end gap-3">
          <Button
            type="button"
            variant="secondary"
            onClick={() => setConfirmComplete(null)}
            disabled={isCompleting}
          >
            キャンセル
          </Button>
          <Button
            type="button"
            variant="primary"
            onClick={handleConfirmComplete}
            disabled={isCompleting}
          >
            {isCompleting ? "完了処理中..." : "全て完了にする"}
          </Button>
        </div>
      </Modal>
    </div>
  );
}
