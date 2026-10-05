"use client";

import { use, useEffect, useState } from "react";
import Link from "next/link";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { WorkspaceMembers } from "@/components/workspace/WorkspaceMembers";
import { apiFetch } from "@/lib/api";
import type { Workspace } from "@/types/workspace";

export default function WorkspaceSettingsPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const workspaceId = Number(id);
  const [workspace, setWorkspace] = useState<Workspace | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<Workspace>(`/workspaces/${workspaceId}`)
      .then(setWorkspace)
      .catch(() => setError("ワークスペース情報の取得に失敗しました。"));
  }, [workspaceId]);

  if (error) {
    return <ErrorMessage message={error} />;
  }

  if (!workspace) {
    return <Loading />;
  }

  const canManage = workspace.myRole === "ADMIN";

  return (
    <div className="mx-auto max-w-2xl">
      <div className="mb-4">
        <Link href="/projects" className="text-sm text-blue-600 hover:underline">
          ← プロジェクト一覧へ
        </Link>
      </div>

      <h1 className="mb-1 text-lg font-bold text-gray-900">{workspace.name}</h1>
      {workspace.description && <p className="mb-4 text-sm text-gray-600">{workspace.description}</p>}

      <section className="rounded-lg bg-white p-6 shadow-sm">
        <h2 className="mb-4 text-base font-semibold text-gray-900">メンバー</h2>
        {!canManage && (
          <p className="mb-3 text-sm text-gray-500">
            メンバーの管理はワークスペース管理者(ADMIN)のみ可能です。
          </p>
        )}
        <WorkspaceMembers workspaceId={workspaceId} canManage={canManage} />
      </section>
    </div>
  );
}
