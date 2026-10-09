"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/common/Button";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { formatDate } from "@/lib/format";
import type { AdminUser } from "@/types/admin";

interface AdminUsersProps {
  /** ログイン中ユーザーの ID(自分自身の剥奪を抑止する)。 */
  currentUserId: number;
}

/** 全ユーザー一覧と System Admin 権限の付与/剥奪(M5 §5)。 */
export function AdminUsers({ currentUserId }: AdminUsersProps) {
  const [users, setUsers] = useState<AdminUser[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);

  async function reload() {
    setUsers(await apiFetch<AdminUser[]>("/system/users"));
  }

  useEffect(() => {
    reload().catch(() => setError("ユーザー一覧の取得に失敗しました。"));
  }, []);

  async function toggleAdmin(user: AdminUser) {
    setError(null);
    setBusyId(user.id);
    try {
      await apiFetch<void>(`/system/admins/${user.id}`, {
        method: user.isSystemAdmin ? "DELETE" : "POST",
      });
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "権限の変更に失敗しました。"));
    } finally {
      setBusyId(null);
    }
  }

  return (
    <section>
      <h2 className="mb-3 text-base font-bold text-gray-900">ユーザー</h2>
      {error && <p className="mb-2 text-sm text-red-600">{error}</p>}
      {!users && <p className="text-sm text-gray-500">読み込み中...</p>}
      {users && (
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr className="border-b border-gray-200 text-left text-gray-500">
              <th className="py-2 pr-3">名前</th>
              <th className="py-2 pr-3">メール</th>
              <th className="py-2 pr-3">権限</th>
              <th className="py-2 pr-3">登録日</th>
              <th className="py-2 pr-3">操作</th>
            </tr>
          </thead>
          <tbody>
            {users.map((user) => (
              <tr key={user.id} className="border-b border-gray-100" data-testid={`user-row-${user.id}`}>
                <td className="py-2 pr-3 text-gray-900">{user.name}</td>
                <td className="py-2 pr-3 text-gray-700">{user.email}</td>
                <td className="py-2 pr-3">
                  {user.isSystemAdmin ? (
                    <span className="rounded bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-700">
                      System Admin
                    </span>
                  ) : (
                    <span className="text-xs text-gray-500">一般</span>
                  )}
                </td>
                <td className="py-2 pr-3 text-gray-700">{formatDate(user.createdAt)}</td>
                <td className="py-2 pr-3">
                  {user.id === currentUserId ? (
                    <span className="text-xs text-gray-400">(自分)</span>
                  ) : (
                    <Button
                      type="button"
                      variant={user.isSystemAdmin ? "danger" : "secondary"}
                      disabled={busyId === user.id}
                      onClick={() => toggleAdmin(user)}
                    >
                      {user.isSystemAdmin ? "権限を剥奪" : "権限を付与"}
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
