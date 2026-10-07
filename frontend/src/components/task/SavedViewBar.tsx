"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { Modal } from "@/components/common/Modal";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { EMPTY_FILTERS, type TaskFilters } from "@/components/task/TaskFilterBar";
import type { SavedView, SavedViewRequestBody } from "@/types/savedView";

interface SavedViewBarProps {
  workspaceId: number | null;
  views: SavedView[];
  filters: TaskFilters;
  viewType: "list" | "board";
  onApply: (filters: TaskFilters, viewType: "list" | "board") => void;
  onViewsChanged: (views: SavedView[]) => void;
}

/** 保存ビューの filters(API形式)を画面のフィルタ状態に変換する。 */
function toFilters(v: SavedView): TaskFilters {
  const f = v.filters;
  return {
    keyword: f.keyword ?? "",
    status: f.status ?? [],
    assigneeId: f.assigneeId ?? "",
    priority: f.priority ?? [],
    labelId: f.labelId ?? [],
    sort: f.sort ?? "",
  };
}

/** 画面のフィルタ状態を保存用の filters(API形式)に変換する。 */
function toBody(filters: TaskFilters, name: string, viewType: "list" | "board", isShared: boolean): SavedViewRequestBody {
  return {
    name,
    viewType: viewType === "board" ? "BOARD" : "LIST",
    isShared,
    filters: {
      status: filters.status,
      assigneeId: filters.assigneeId || null,
      priority: filters.priority,
      labelId: filters.labelId,
      keyword: filters.keyword || null,
      sort: filters.sort || null,
    },
  };
}

export function SavedViewBar({
  workspaceId,
  views,
  filters,
  viewType,
  onApply,
  onViewsChanged,
}: SavedViewBarProps) {
  const [selectedId, setSelectedId] = useState<number | "">("");
  const [isSaving, setIsSaving] = useState(false);
  const [saveName, setSaveName] = useState("");
  const [saveShared, setSaveShared] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (workspaceId === null) {
    return null;
  }

  async function reload() {
    onViewsChanged(await apiFetch<SavedView[]>(`/workspaces/${workspaceId}/views`));
  }

  function applyView(id: number | "") {
    setSelectedId(id);
    if (id === "") {
      onApply(EMPTY_FILTERS, viewType);
      return;
    }
    const view = views.find((v) => v.id === id);
    if (view) {
      onApply(toFilters(view), view.viewType === "BOARD" ? "board" : "list");
    }
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!saveName.trim()) {
      setError("ビュー名を入力してください。");
      return;
    }
    setError(null);
    try {
      const created = await apiFetch<SavedView>(`/workspaces/${workspaceId}/views`, {
        method: "POST",
        body: JSON.stringify(toBody(filters, saveName.trim(), viewType, saveShared)),
      });
      await reload();
      setSelectedId(created.id);
      setIsSaving(false);
      setSaveName("");
      setSaveShared(false);
    } catch (err) {
      setError(formatApiErrorMessage(err, "ビューの保存に失敗しました。"));
    }
  }

  async function handleDelete() {
    if (selectedId === "") return;
    try {
      await apiFetch<void>(`/workspaces/${workspaceId}/views/${selectedId}`, { method: "DELETE" });
      await reload();
      applyView("");
    } catch (err) {
      setError(formatApiErrorMessage(err, "ビューの削除に失敗しました。"));
    }
  }

  const selected = views.find((v) => v.id === selectedId);

  return (
    <div className="mb-3 flex flex-wrap items-center gap-2 text-sm">
      <label htmlFor="savedView" className="text-gray-600">
        ビュー:
      </label>
      <select
        id="savedView"
        aria-label="保存ビュー"
        value={selectedId}
        onChange={(e) => applyView(e.target.value === "" ? "" : Number(e.target.value))}
        className="rounded-md border border-gray-300 px-2 py-1 text-sm"
      >
        <option value="">（なし）</option>
        {views.map((v) => (
          <option key={v.id} value={v.id}>
            {v.name}
            {v.isShared ? "（共有）" : ""}
          </option>
        ))}
      </select>

      <Button type="button" variant="secondary" onClick={() => setIsSaving(true)}>
        現在の条件を保存
      </Button>

      {selected && selected.isOwner && (
        <button type="button" onClick={handleDelete} className="text-red-600 hover:underline">
          このビューを削除
        </button>
      )}

      {error && <span className="text-red-600">{error}</span>}

      <Modal isOpen={isSaving} title="ビューを保存" onClose={() => setIsSaving(false)}>
        <form onSubmit={handleSave} className="space-y-4">
          <Input
            id="saveViewName"
            label="ビュー名"
            value={saveName}
            onChange={(e) => setSaveName(e.target.value)}
          />
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" checked={saveShared} onChange={(e) => setSaveShared(e.target.checked)} />
            このワークスペースのメンバーに共有する
          </label>
          <div className="flex justify-end gap-3">
            <Button type="button" variant="secondary" onClick={() => setIsSaving(false)}>
              キャンセル
            </Button>
            <Button type="submit" variant="primary">
              保存
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
