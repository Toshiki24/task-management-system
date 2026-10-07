"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { apiFetch } from "@/lib/api";
import { formatDate } from "@/lib/format";
import { priorityLabel } from "@/lib/taskLabels";
import type { MyTask, Paged } from "@/types/myTask";

const PAGE_SIZE = 20;

type Bucket = "overdue" | "soon" | "other";

/** 期限で 期限超過 / 今日・今週(7日以内) / その他 に区分する。 */
function bucketOf(dueDate: string | null): Bucket {
  if (!dueDate) return "other";
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const due = new Date(`${dueDate}T00:00:00`);
  const diffDays = Math.round((due.getTime() - today.getTime()) / 86_400_000);
  if (diffDays < 0) return "overdue";
  if (diffDays <= 7) return "soon";
  return "other";
}

const SECTIONS: { key: Bucket; title: string; accent: string }[] = [
  { key: "overdue", title: "期限超過", accent: "text-red-600" },
  { key: "soon", title: "今日・今週", accent: "text-amber-600" },
  { key: "other", title: "その他", accent: "text-gray-600" },
];

export default function MyTasksPage() {
  const [data, setData] = useState<Paged<MyTask> | null>(null);
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState("");
  const [applied, setApplied] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) });
    if (applied.trim()) params.set("keyword", applied.trim());
    setData(null);
    apiFetch<Paged<MyTask>>(`/me/tasks?${params.toString()}`)
      .then(setData)
      .catch(() => setError("タスクの取得に失敗しました。"));
  }, [page, applied]);

  if (error) {
    return <ErrorMessage message={error} />;
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <div>
      <div className="mb-4 flex items-center justify-between gap-4">
        <h1 className="text-lg font-bold text-gray-900">My Tasks</h1>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            setPage(1);
            setApplied(keyword);
          }}
        >
          <input
            aria-label="キーワード"
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            placeholder="キーワードで検索"
            className="rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </form>
      </div>

      {!data ? (
        <Loading />
      ) : data.total === 0 ? (
        <p className="text-sm text-gray-500">担当しているタスクはありません。</p>
      ) : (
        <>
          {SECTIONS.map((section) => {
            const items = data.items.filter((t) => bucketOf(t.dueDate) === section.key);
            if (items.length === 0) return null;
            return (
              <section key={section.key} className="mb-6">
                <h2 className={`mb-2 text-sm font-semibold ${section.accent}`}>
                  {section.title}（{items.length}）
                </h2>
                <ul className="divide-y divide-gray-100 rounded-lg bg-white shadow-sm">
                  {items.map((task) => (
                    <li key={task.id}>
                      <Link
                        href={`/tasks/${task.id}`}
                        className="flex items-center justify-between gap-3 px-4 py-3 text-sm hover:bg-gray-50"
                      >
                        <span className="min-w-0">
                          <span className="block truncate font-medium text-gray-900">{task.title}</span>
                          <span className="block truncate text-xs text-gray-500">
                            {task.workspaceName} / {task.projectName}
                          </span>
                        </span>
                        <span className="flex shrink-0 items-center gap-3 text-xs text-gray-500">
                          {task.labels.map((label) => (
                            <span
                              key={label.id}
                              className="rounded-full px-1.5 py-0.5 text-[10px] text-white"
                              style={{ backgroundColor: label.color ?? "#6b7280" }}
                            >
                              {label.name}
                            </span>
                          ))}
                          <span>優先度: {priorityLabel(task.priority)}</span>
                          <span>{formatDate(task.dueDate)}</span>
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              </section>
            );
          })}

          {totalPages > 1 && (
            <div className="mt-4 flex items-center justify-center gap-4 text-sm">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
                className="rounded-md border border-gray-300 px-3 py-1.5 disabled:opacity-40"
              >
                前へ
              </button>
              <span className="text-gray-600">
                {page} / {totalPages}
              </span>
              <button
                type="button"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
                className="rounded-md border border-gray-300 px-3 py-1.5 disabled:opacity-40"
              >
                次へ
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
