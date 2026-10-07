"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Input } from "@/components/common/Input";
import { Modal } from "@/components/common/Modal";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import type { Label, LabelRequestBody } from "@/types/label";

interface LabelManagerProps {
  workspaceId: number;
  /** 呼び出しユーザーがラベルの追加・編集・削除を行えるか(WS Admin 以上) */
  canManage: boolean;
}

export function LabelManager({ workspaceId, canManage }: LabelManagerProps) {
  const [labels, setLabels] = useState<Label[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [isAdding, setIsAdding] = useState(false);
  const [newName, setNewName] = useState("");
  const [newColor, setNewColor] = useState("#3b82f6");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState<Label | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  useEffect(() => {
    apiFetch<Label[]>(`/workspaces/${workspaceId}/labels`)
      .then(setLabels)
      .catch(() => setError("ラベルの取得に失敗しました。"));
  }, [workspaceId]);

  async function reload() {
    setLabels(await apiFetch<Label[]>(`/workspaces/${workspaceId}/labels`));
  }

  async function handleAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    if (!newName.trim()) {
      setError("ラベル名を入力してください。");
      return;
    }
    setIsSubmitting(true);
    try {
      const body: LabelRequestBody = { name: newName.trim(), color: newColor };
      await apiFetch(`/workspaces/${workspaceId}/labels`, { method: "POST", body: JSON.stringify(body) });
      await reload();
      setIsAdding(false);
      setNewName("");
      setNewColor("#3b82f6");
    } catch (err) {
      setError(formatApiErrorMessage(err, "ラベルの追加に失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDeleteConfirmed() {
    if (!deleteTarget) return;
    setIsDeleting(true);
    setError(null);
    try {
      await apiFetch<void>(`/workspaces/${workspaceId}/labels/${deleteTarget.id}`, { method: "DELETE" });
      await reload();
      setDeleteTarget(null);
    } catch (err) {
      setError(formatApiErrorMessage(err, "ラベルの削除に失敗しました。"));
    } finally {
      setIsDeleting(false);
    }
  }

  if (!labels && !error) {
    return <p className="text-sm text-gray-500">読み込み中...</p>;
  }

  return (
    <div>
      {error && <ErrorMessage message={error} />}

      {labels && labels.length > 0 && (
        <ul className="mb-4 flex flex-wrap gap-2">
          {labels.map((label) => (
            <li
              key={label.id}
              className="flex items-center gap-2 rounded-full border border-gray-200 px-3 py-1 text-sm"
            >
              <span
                aria-hidden
                className="inline-block h-3 w-3 rounded-full"
                style={{ backgroundColor: label.color ?? "#9ca3af" }}
              />
              <span className="text-gray-900">{label.name}</span>
              {canManage && (
                <button
                  type="button"
                  aria-label={`${label.name} を削除`}
                  onClick={() => setDeleteTarget(label)}
                  className="text-red-600 hover:underline"
                >
                  ×
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {labels && labels.length === 0 && <p className="mb-4 text-sm text-gray-500">ラベルがありません。</p>}

      {canManage && !isAdding && (
        <Button type="button" variant="secondary" onClick={() => setIsAdding(true)}>
          ラベルを追加
        </Button>
      )}

      {canManage && isAdding && (
        <form onSubmit={handleAdd} className="flex flex-wrap items-end gap-3 rounded-md border border-gray-200 p-4">
          <Input
            id="newLabelName"
            label="ラベル名"
            value={newName}
            onChange={(event) => setNewName(event.target.value)}
            placeholder="bug"
          />
          <div>
            <label htmlFor="newLabelColor" className="mb-1 block text-sm font-medium text-gray-700">
              色
            </label>
            <input
              id="newLabelColor"
              type="color"
              value={newColor}
              onChange={(event) => setNewColor(event.target.value)}
              className="h-9 w-16 rounded border border-gray-300"
            />
          </div>
          <Button type="button" variant="secondary" onClick={() => setIsAdding(false)} disabled={isSubmitting}>
            キャンセル
          </Button>
          <Button type="submit" variant="primary" disabled={isSubmitting}>
            {isSubmitting ? "追加中..." : "追加"}
          </Button>
        </form>
      )}

      <Modal isOpen={deleteTarget !== null} title="ラベルの削除" onClose={() => setDeleteTarget(null)}>
        <p>「{deleteTarget?.name}」を削除しますか？このラベルが付いたタスクからも外れます。</p>
        <div className="mt-6 flex justify-end gap-3">
          <Button type="button" variant="secondary" onClick={() => setDeleteTarget(null)} disabled={isDeleting}>
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
