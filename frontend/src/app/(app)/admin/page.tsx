"use client";

import { useEffect, useState } from "react";
import { AdminAuditLogs } from "@/components/admin/AdminAuditLogs";
import { AdminUsers } from "@/components/admin/AdminUsers";
import { AdminWorkspaces } from "@/components/admin/AdminWorkspaces";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { apiFetch } from "@/lib/api";
import { fetchCurrentUser } from "@/lib/auth";
import type { SystemStats } from "@/types/admin";

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4">
      <p className="text-sm text-gray-500">{label}</p>
      <p className="mt-1 text-2xl font-bold text-gray-900">{value}</p>
    </div>
  );
}

export default function AdminPage() {
  const [authState, setAuthState] = useState<
    { status: "checking" } | { status: "forbidden" } | { status: "ready"; userId: number }
  >({ status: "checking" });
  const [stats, setStats] = useState<SystemStats | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchCurrentUser()
      .then((user) => {
        if (user?.isSystemAdmin) {
          setAuthState({ status: "ready", userId: user.id });
        } else {
          setAuthState({ status: "forbidden" });
        }
      })
      .catch(() => setAuthState({ status: "forbidden" }));
  }, []);

  useEffect(() => {
    if (authState.status !== "ready") return;
    apiFetch<SystemStats>("/system/stats")
      .then(setStats)
      .catch(() => setError("統計の取得に失敗しました。"));
  }, [authState.status]);

  if (authState.status === "checking") return <Loading />;
  if (authState.status === "forbidden") {
    return <ErrorMessage message="この画面には System Admin のみアクセスできます。" />;
  }

  return (
    <div className="mx-auto max-w-4xl space-y-8">
      <h1 className="text-lg font-bold text-gray-900">管理コンソール</h1>

      <section>
        <h2 className="mb-3 text-base font-bold text-gray-900">システム統計</h2>
        {error && <ErrorMessage message={error} />}
        {!stats && !error && <p className="text-sm text-gray-500">読み込み中...</p>}
        {stats && (
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
            <Stat label="ワークスペース" value={stats.workspaceCount} />
            <Stat label="プロジェクト" value={stats.projectCount} />
            <Stat label="タスク" value={stats.taskCount} />
            <Stat label="ユーザー" value={stats.userCount} />
            <Stat label="System Admin" value={stats.systemAdminCount} />
            <Stat label="直近7日の活動" value={stats.recentActivityCount} />
          </div>
        )}
      </section>

      <AdminWorkspaces />
      <AdminUsers currentUserId={authState.userId} />
      <AdminAuditLogs />
    </div>
  );
}
