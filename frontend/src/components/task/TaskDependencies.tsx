"use client";

import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { Button } from "@/components/common/Button";
import { Select } from "@/components/common/Select";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import type { DependencyLink, Task, TaskDependencies as Deps } from "@/types/task";

interface TaskDependenciesProps {
  taskId: string;
  projectId: number;
  /** 状態キー→表示名(日本語)。 */
  statusLabels?: Record<string, string>;
}

// 経路タスクを基準にした依存の向き(バックエンドの DependencyRelations と対応)
const RELATION_OPTIONS = [
  { value: "BLOCKED_BY", label: "このタスクは次のタスクを待つ(ブロックされる)" },
  { value: "BLOCKS", label: "このタスクが次のタスクを待たせる(ブロックする)" },
];

export function TaskDependencies({ taskId, projectId, statusLabels = {} }: TaskDependenciesProps) {
  const [deps, setDeps] = useState<Deps | null>(null);
  const [candidates, setCandidates] = useState<Task[]>([]);
  const [relation, setRelation] = useState("BLOCKED_BY");
  const [targetId, setTargetId] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<Deps>(`/tasks/${taskId}/dependencies`)
      .then(setDeps)
      .catch(() => setError("依存関係の取得に失敗しました。"));
    // 追加候補は同一プロジェクトの他タスク(自分自身は除く)
    apiFetch<Task[]>(`/projects/${projectId}/tasks`)
      .then((tasks) => setCandidates(tasks.filter((t) => String(t.id) !== String(taskId))))
      .catch(() => {
        // 候補が取れなくても一覧表示自体は続ける
      });
  }, [taskId, projectId]);

  async function reload() {
    setDeps(await apiFetch<Deps>(`/tasks/${taskId}/dependencies`));
  }

  async function handleAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!targetId) return;
    setError(null);
    try {
      await apiFetch(`/tasks/${taskId}/dependencies`, {
        method: "POST",
        body: JSON.stringify({ taskId: Number(targetId), relation }),
      });
      setTargetId("");
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "依存関係の追加に失敗しました。"));
    }
  }

  async function handleRemove(dependencyId: number) {
    setError(null);
    try {
      await apiFetch(`/tasks/${taskId}/dependencies/${dependencyId}`, { method: "DELETE" });
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "依存関係の削除に失敗しました。"));
    }
  }

  // 未完了のブロッカーが残っていれば警告する
  const openBlockers = deps?.blockedBy.filter((d) => !d.isClosed) ?? [];

  function renderLinks(links: DependencyLink[], emptyText: string) {
    if (links.length === 0) {
      return <p className="text-sm text-gray-500">{emptyText}</p>;
    }
    return (
      <ul className="divide-y divide-gray-100 rounded-md border border-gray-200">
        {links.map((link) => (
          <li key={link.dependencyId} className="flex items-center justify-between gap-2 px-3 py-2">
            <Link href={`/tasks/${link.taskId}`} className="flex items-center gap-2 truncate text-sm hover:underline">
              <span className={`truncate ${link.isClosed ? "text-gray-400 line-through" : "text-gray-900"}`}>
                {link.title}
              </span>
              <span className="shrink-0 text-xs text-gray-500">{statusLabels[link.status] ?? link.status}</span>
            </Link>
            <button
              type="button"
              onClick={() => handleRemove(link.dependencyId)}
              className="shrink-0 text-xs text-red-600 hover:underline"
              aria-label="依存関係を削除"
            >
              削除
            </button>
          </li>
        ))}
      </ul>
    );
  }

  return (
    <div>
      <h2 className="mb-2 text-base font-bold text-gray-900">依存関係</h2>

      {error && <p className="mb-2 text-sm text-red-600">{error}</p>}

      {openBlockers.length > 0 && (
        <p className="mb-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          ⚠ 未完了のブロッカーが {openBlockers.length} 件あります。先に完了させる必要があります。
        </p>
      )}

      <div className="mb-4 space-y-3">
        <div>
          <p className="mb-1 text-sm font-medium text-gray-700">ブロックされている(先に完了が必要)</p>
          {deps ? renderLinks(deps.blockedBy, "ブロックしているタスクはありません。") : null}
        </div>
        <div>
          <p className="mb-1 text-sm font-medium text-gray-700">ブロックしている(このタスク待ち)</p>
          {deps ? renderLinks(deps.blocking, "ブロックしているタスクはありません。") : null}
        </div>
      </div>

      <form onSubmit={handleAdd} className="space-y-2">
        <Select
          id="dependencyRelation"
          aria-label="依存の種類"
          value={relation}
          onChange={(e) => setRelation(e.target.value)}
          options={RELATION_OPTIONS}
        />
        <div className="flex items-end gap-2">
          <div className="flex-1">
            <Select
              id="dependencyTarget"
              aria-label="対象タスク"
              value={targetId}
              onChange={(e) => setTargetId(e.target.value)}
              options={[
                { value: "", label: "タスクを選択" },
                ...candidates.map((t) => ({ value: String(t.id), label: t.title })),
              ]}
            />
          </div>
          <Button type="submit" variant="secondary" disabled={!targetId}>
            追加
          </Button>
        </div>
      </form>
    </div>
  );
}
