"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { apiFetch } from "@/lib/api";
import type { Metrics } from "@/types/metrics";

export default function ProjectMetricsPage() {
  const params = useParams<{ id: string }>();
  const projectId = params.id;
  const [metrics, setMetrics] = useState<Metrics | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<Metrics>(`/projects/${projectId}/metrics`)
      .then(setMetrics)
      .catch(() => setError("指標の取得に失敗しました。"));
  }, [projectId]);

  if (error) return <ErrorMessage message={error} />;
  if (!metrics) return <Loading />;

  const maxStatus = Math.max(1, ...metrics.statusCounts.map((s) => s.count));
  const maxLoad = Math.max(1, ...metrics.assigneeLoads.map((a) => a.openCount));
  const completionPct = Math.round(metrics.completionRate * 100);

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-bold text-gray-900">ダッシュボード</h1>
        <Link href={`/projects/${projectId}`} className="text-sm text-blue-600 hover:underline">
          ← プロジェクト詳細へ
        </Link>
      </div>

      {/* サマリ */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <Stat label="タスク総数" value={metrics.total} />
        <Stat label="完了" value={metrics.doneCount} />
        <Stat label="完了率" value={`${completionPct}%`} />
        <Stat label="期限超過" value={metrics.overdueCount} accent={metrics.overdueCount > 0} />
      </div>

      {/* 状態別件数 */}
      <section className="rounded-lg bg-white p-6 shadow-sm">
        <h2 className="mb-4 text-base font-semibold text-gray-900">状態別の件数</h2>
        {metrics.total === 0 ? (
          <p className="text-sm text-gray-500">タスクがありません。</p>
        ) : (
          <ul className="space-y-2">
            {metrics.statusCounts.map((s) => (
              <li key={s.key} className="flex items-center gap-3 text-sm">
                <span className="w-24 shrink-0 text-gray-700">{s.name}</span>
                <div className="h-4 flex-1 overflow-hidden rounded bg-gray-100">
                  <div
                    className="h-full rounded bg-blue-500"
                    style={{ width: `${(s.count / maxStatus) * 100}%` }}
                    role="img"
                    aria-label={`${s.name} ${s.count}件`}
                  />
                </div>
                <span className="w-8 shrink-0 text-right text-gray-900">{s.count}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* 担当別の負荷 */}
      <section className="rounded-lg bg-white p-6 shadow-sm">
        <h2 className="mb-1 text-base font-semibold text-gray-900">担当別の負荷</h2>
        <p className="mb-4 text-xs text-gray-500">未完了タスクの件数と見積ポイントの合計です。</p>
        {metrics.assigneeLoads.length === 0 ? (
          <p className="text-sm text-gray-500">未完了のタスクはありません。</p>
        ) : (
          <ul className="space-y-2">
            {metrics.assigneeLoads.map((a) => (
              <li key={a.assigneeId ?? "none"} className="flex items-center gap-3 text-sm">
                <span className="w-28 shrink-0 truncate text-gray-700">{a.name}</span>
                <div className="h-4 flex-1 overflow-hidden rounded bg-gray-100">
                  <div
                    className="h-full rounded bg-emerald-500"
                    style={{ width: `${(a.openCount / maxLoad) * 100}%` }}
                    role="img"
                    aria-label={`${a.name} 未完了${a.openCount}件`}
                  />
                </div>
                <span className="w-24 shrink-0 text-right text-gray-900">
                  {a.openCount}件・{a.estimatePoints}pt
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function Stat({ label, value, accent = false }: { label: string; value: number | string; accent?: boolean }) {
  return (
    <div className="rounded-lg bg-white p-4 shadow-sm">
      <div className="text-xs text-gray-500">{label}</div>
      <div className={`mt-1 text-2xl font-bold ${accent ? "text-red-600" : "text-gray-900"}`}>{value}</div>
    </div>
  );
}
