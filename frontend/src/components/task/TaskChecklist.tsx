"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import type { ChecklistItem } from "@/types/task";

interface TaskChecklistProps {
  taskId: number | string;
}

export function TaskChecklist({ taskId }: TaskChecklistProps) {
  const [items, setItems] = useState<ChecklistItem[] | null>(null);
  const [content, setContent] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<ChecklistItem[]>(`/tasks/${taskId}/checklist`)
      .then(setItems)
      .catch(() => setError("チェックリストの取得に失敗しました。"));
  }, [taskId]);

  async function reload() {
    setItems(await apiFetch<ChecklistItem[]>(`/tasks/${taskId}/checklist`));
  }

  async function handleAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!content.trim()) return;
    setError(null);
    try {
      await apiFetch(`/tasks/${taskId}/checklist`, {
        method: "POST",
        body: JSON.stringify({ content: content.trim() }),
      });
      setContent("");
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "項目の追加に失敗しました。"));
    }
  }

  async function toggle(item: ChecklistItem) {
    setError(null);
    try {
      await apiFetch(`/tasks/${taskId}/checklist/${item.id}`, {
        method: "PATCH",
        body: JSON.stringify({ content: item.content, isDone: !item.isDone }),
      });
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "更新に失敗しました。"));
    }
  }

  async function remove(item: ChecklistItem) {
    setError(null);
    try {
      await apiFetch<void>(`/tasks/${taskId}/checklist/${item.id}`, { method: "DELETE" });
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "削除に失敗しました。"));
    }
  }

  const done = items?.filter((i) => i.isDone).length ?? 0;
  const total = items?.length ?? 0;

  return (
    <div>
      <h2 className="mb-2 flex items-center gap-2 text-base font-bold text-gray-900">
        チェックリスト
        {total > 0 && (
          <span className="rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-600">
            {done}/{total}
          </span>
        )}
      </h2>

      {error && <p className="mb-2 text-sm text-red-600">{error}</p>}

      {items && items.length > 0 && (
        <ul className="mb-3 space-y-1">
          {items.map((item) => (
            <li key={item.id} className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={item.isDone}
                onChange={() => toggle(item)}
                aria-label={item.content}
              />
              <span className={item.isDone ? "text-gray-400 line-through" : "text-gray-900"}>{item.content}</span>
              <button
                type="button"
                onClick={() => remove(item)}
                aria-label={`${item.content} を削除`}
                className="ml-auto text-red-600 hover:underline"
              >
                削除
              </button>
            </li>
          ))}
        </ul>
      )}

      <form onSubmit={handleAdd} className="flex items-end gap-2">
        <div className="flex-1">
          <Input
            id="newChecklistItem"
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder="項目を追加"
          />
        </div>
        <Button type="submit" variant="secondary">
          追加
        </Button>
      </form>
    </div>
  );
}
