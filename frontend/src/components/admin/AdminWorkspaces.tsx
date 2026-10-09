"use client";

import { useEffect, useState } from "react";
import { apiFetch } from "@/lib/api";
import { formatDate } from "@/lib/format";
import type { AdminWorkspace } from "@/types/admin";

/** 全ワークスペース一覧(M5 §5)。 */
export function AdminWorkspaces() {
  const [workspaces, setWorkspaces] = useState<AdminWorkspace[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<AdminWorkspace[]>("/system/workspaces")
      .then(setWorkspaces)
      .catch(() => setError("ワークスペース一覧の取得に失敗しました。"));
  }, []);

  return (
    <section>
      <h2 className="mb-3 text-base font-bold text-gray-900">ワークスペース</h2>
      {error && <p className="mb-2 text-sm text-red-600">{error}</p>}
      {!workspaces && <p className="text-sm text-gray-500">読み込み中...</p>}
      {workspaces && (
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr className="border-b border-gray-200 text-left text-gray-500">
              <th className="py-2 pr-3">名前</th>
              <th className="py-2 pr-3">メンバー数</th>
              <th className="py-2 pr-3">プロジェクト数</th>
              <th className="py-2 pr-3">状態</th>
              <th className="py-2 pr-3">作成日</th>
            </tr>
          </thead>
          <tbody>
            {workspaces.map((ws) => (
              <tr key={ws.id} className="border-b border-gray-100">
                <td className="py-2 pr-3 text-gray-900">{ws.name}</td>
                <td className="py-2 pr-3 text-gray-700">{ws.memberCount}</td>
                <td className="py-2 pr-3 text-gray-700">{ws.projectCount}</td>
                <td className="py-2 pr-3">
                  {ws.isArchived ? (
                    <span className="text-xs text-gray-500">アーカイブ済み</span>
                  ) : (
                    <span className="text-xs text-green-700">有効</span>
                  )}
                </td>
                <td className="py-2 pr-3 text-gray-700">{formatDate(ws.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
