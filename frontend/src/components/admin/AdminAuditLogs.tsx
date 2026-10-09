"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/common/Button";
import { Select } from "@/components/common/Select";
import { apiFetch } from "@/lib/api";
import { auditActionLabel, auditTargetLabel, AUDIT_ACTION_OPTIONS } from "@/lib/auditLabels";
import { formatDateTime } from "@/lib/format";
import type { AuditLogPage } from "@/types/admin";

const PAGE_SIZE = 20;

/** 監査ログ閲覧(フィルタ・ページング。M5 §5)。 */
export function AdminAuditLogs() {
  const [action, setAction] = useState("");
  const [offset, setOffset] = useState(0);
  const [page, setPage] = useState<AuditLogPage | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const params = new URLSearchParams({ offset: String(offset), limit: String(PAGE_SIZE) });
    if (action) params.set("action", action);
    apiFetch<AuditLogPage>(`/system/audit-logs?${params.toString()}`)
      .then(setPage)
      .catch(() => setError("監査ログの取得に失敗しました。"));
  }, [action, offset]);

  const total = page?.total ?? 0;
  const hasPrev = offset > 0;
  const hasNext = offset + PAGE_SIZE < total;

  return (
    <section>
      <h2 className="mb-3 text-base font-bold text-gray-900">監査ログ</h2>

      <div className="mb-3 flex items-end gap-3">
        <div className="w-72">
          <Select
            id="audit-action-filter"
            label="アクションで絞り込み"
            value={action}
            options={AUDIT_ACTION_OPTIONS}
            onChange={(e) => {
              setOffset(0);
              setAction(e.target.value);
            }}
          />
        </div>
        <p className="pb-2 text-sm text-gray-500">全 {total} 件</p>
      </div>

      {error && <p className="text-sm text-red-600">{error}</p>}

      {page && page.items.length === 0 && (
        <p className="rounded-md border border-gray-200 p-4 text-sm text-gray-500">
          該当する監査ログはありません。
        </p>
      )}

      {page && page.items.length > 0 && (
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr className="border-b border-gray-200 text-left text-gray-500">
              <th className="py-2 pr-3">日時</th>
              <th className="py-2 pr-3">アクター</th>
              <th className="py-2 pr-3">アクション</th>
              <th className="py-2 pr-3">対象</th>
            </tr>
          </thead>
          <tbody>
            {page.items.map((log) => (
              <tr key={log.id} className="border-b border-gray-100">
                <td className="py-2 pr-3 text-gray-700">{formatDateTime(log.createdAt)}</td>
                <td className="py-2 pr-3 text-gray-700">{log.actorName ?? `#${log.actorUserId}`}</td>
                <td className="py-2 pr-3 text-gray-900">{auditActionLabel(log.action)}</td>
                <td className="py-2 pr-3 text-gray-700">
                  {auditTargetLabel(log.targetType)} #{log.targetId}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <div className="mt-3 flex items-center gap-3">
        <Button
          type="button"
          variant="secondary"
          disabled={!hasPrev}
          onClick={() => setOffset((o) => Math.max(0, o - PAGE_SIZE))}
        >
          前へ
        </Button>
        <Button
          type="button"
          variant="secondary"
          disabled={!hasNext}
          onClick={() => setOffset((o) => o + PAGE_SIZE)}
        >
          次へ
        </Button>
      </div>
    </section>
  );
}
