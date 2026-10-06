"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Input } from "@/components/common/Input";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { Select } from "@/components/common/Select";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import {
  WORKFLOW_CATEGORY_LABELS,
  type CreateWorkflowStateBody,
  type UpdateWorkflowStateBody,
  type WorkflowState,
  type WorkflowStateCategory,
} from "@/types/workflow";

const CATEGORY_OPTIONS: { value: WorkflowStateCategory; label: string }[] = (
  ["BACKLOG", "TODO", "IN_PROGRESS", "DONE", "CANCELLED"] as const
).map((value) => ({ value, label: WORKFLOW_CATEGORY_LABELS[value] }));

interface WorkflowStatesProps {
  workspaceId: number;
  /** 呼び出しユーザーが状態の追加・編集・削除を行えるか(WS Admin 以上) */
  canManage: boolean;
}

export function WorkflowStates({ workspaceId, canManage }: WorkflowStatesProps) {
  const [states, setStates] = useState<WorkflowState[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [isAdding, setIsAdding] = useState(false);
  const [newKey, setNewKey] = useState("");
  const [newName, setNewName] = useState("");
  const [newCategory, setNewCategory] = useState<WorkflowStateCategory>("IN_PROGRESS");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState<WorkflowState | null>(null);
  const [moveToKey, setMoveToKey] = useState("");
  const [isDeleting, setIsDeleting] = useState(false);

  useEffect(() => {
    apiFetch<WorkflowState[]>(`/workspaces/${workspaceId}/workflow-states`)
      .then(setStates)
      .catch(() => setError("ワークフローの取得に失敗しました。"));
  }, [workspaceId]);

  async function reload() {
    setStates(await apiFetch<WorkflowState[]>(`/workspaces/${workspaceId}/workflow-states`));
  }

  async function handleAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    if (!newKey.trim() || !newName.trim()) {
      setError("状態キーと状態名を入力してください。");
      return;
    }

    setIsSubmitting(true);
    try {
      const body: CreateWorkflowStateBody = {
        key: newKey.trim().toUpperCase(),
        name: newName.trim(),
        category: newCategory,
      };
      await apiFetch(`/workspaces/${workspaceId}/workflow-states`, {
        method: "POST",
        body: JSON.stringify(body),
      });
      await reload();
      setIsAdding(false);
      setNewKey("");
      setNewName("");
      setNewCategory("IN_PROGRESS");
    } catch (err) {
      setError(formatApiErrorMessage(err, "状態の追加に失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function move(state: WorkflowState, direction: -1 | 1) {
    setError(null);
    const body: UpdateWorkflowStateBody = {
      name: state.name,
      category: state.category,
      position: state.position + direction,
    };
    try {
      await apiFetch(`/workspaces/${workspaceId}/workflow-states/${state.id}`, {
        method: "PATCH",
        body: JSON.stringify(body),
      });
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "並び替えに失敗しました。"));
    }
  }

  async function handleDeleteConfirmed() {
    if (!deleteTarget) return;
    setIsDeleting(true);
    setError(null);
    try {
      const query = moveToKey ? `?moveTo=${encodeURIComponent(moveToKey)}` : "";
      await apiFetch<void>(`/workspaces/${workspaceId}/workflow-states/${deleteTarget.id}${query}`, {
        method: "DELETE",
      });
      await reload();
      setDeleteTarget(null);
      setMoveToKey("");
    } catch (err) {
      setError(formatApiErrorMessage(err, "状態の削除に失敗しました。"));
    } finally {
      setIsDeleting(false);
    }
  }

  if (!states && !error) {
    return <Loading />;
  }

  return (
    <div>
      {error && <ErrorMessage message={error} />}

      {states && states.length > 0 && (
        <ul className="mb-4 divide-y divide-gray-100">
          {states.map((state, index) => (
            <li
              key={state.id}
              className="flex items-center justify-between gap-3 py-2 text-sm"
            >
              <span className="flex items-center gap-2 text-gray-900">
                <span className="font-medium">{state.name}</span>
                <span className="text-xs text-gray-500">
                  ({state.key} ・ {WORKFLOW_CATEGORY_LABELS[state.category]})
                </span>
                {state.isDefault && (
                  <span className="rounded bg-blue-50 px-1.5 py-0.5 text-xs text-blue-700">既定</span>
                )}
              </span>
              {canManage && (
                <span className="flex items-center gap-2">
                  <button
                    type="button"
                    aria-label={`${state.name} を上へ`}
                    onClick={() => move(state, -1)}
                    disabled={index === 0}
                    className="text-gray-500 hover:text-gray-800 disabled:opacity-30"
                  >
                    ↑
                  </button>
                  <button
                    type="button"
                    aria-label={`${state.name} を下へ`}
                    onClick={() => move(state, 1)}
                    disabled={index === states.length - 1}
                    className="text-gray-500 hover:text-gray-800 disabled:opacity-30"
                  >
                    ↓
                  </button>
                  {!state.isDefault && (
                    <button
                      type="button"
                      onClick={() => setDeleteTarget(state)}
                      className="text-red-600 hover:underline"
                    >
                      削除
                    </button>
                  )}
                </span>
              )}
            </li>
          ))}
        </ul>
      )}

      {canManage && !isAdding && (
        <Button type="button" variant="secondary" onClick={() => setIsAdding(true)}>
          状態を追加
        </Button>
      )}

      {canManage && isAdding && (
        <form
          onSubmit={handleAdd}
          className="flex flex-wrap items-end gap-3 rounded-md border border-gray-200 p-4"
        >
          <Input
            id="newWorkflowStateKey"
            label="状態キー(英大文字)"
            value={newKey}
            onChange={(event) => setNewKey(event.target.value)}
            placeholder="REVIEW"
          />
          <Input
            id="newWorkflowStateName"
            label="状態名"
            value={newName}
            onChange={(event) => setNewName(event.target.value)}
            placeholder="レビュー中"
          />
          <Select
            id="newWorkflowStateCategory"
            label="カテゴリ"
            value={newCategory}
            onChange={(event) => setNewCategory(event.target.value as WorkflowStateCategory)}
            options={CATEGORY_OPTIONS}
          />
          <Button type="button" variant="secondary" onClick={() => setIsAdding(false)} disabled={isSubmitting}>
            キャンセル
          </Button>
          <Button type="submit" variant="primary" disabled={isSubmitting}>
            {isSubmitting ? "追加中..." : "追加"}
          </Button>
        </form>
      )}

      <Modal
        isOpen={deleteTarget !== null}
        title="状態の削除"
        onClose={() => {
          setDeleteTarget(null);
          setMoveToKey("");
        }}
      >
        <p className="mb-3">
          「{deleteTarget?.name}」を削除します。この状態を使っているタスクがある場合は、付け替え先の状態を選んでください。
        </p>
        <Select
          id="workflowStateMoveTo"
          label="付け替え先(使用中の場合)"
          value={moveToKey}
          onChange={(event) => setMoveToKey(event.target.value)}
          options={[
            { value: "", label: "指定しない" },
            ...(states ?? [])
              .filter((s) => s.id !== deleteTarget?.id)
              .map((s) => ({ value: s.key, label: `${s.name} (${s.key})` })),
          ]}
        />
        <div className="mt-6 flex justify-end gap-3">
          <Button
            type="button"
            variant="secondary"
            onClick={() => {
              setDeleteTarget(null);
              setMoveToKey("");
            }}
            disabled={isDeleting}
          >
            キャンセル
          </Button>
          <Button type="button" variant="danger" onClick={handleDeleteConfirmed} disabled={isDeleting}>
            {isDeleting ? "削除中..." : "削除"}
          </Button>
        </div>
      </Modal>
    </div>
  );
}
