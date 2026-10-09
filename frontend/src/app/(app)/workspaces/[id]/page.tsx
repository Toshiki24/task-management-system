"use client";

import { use, useEffect, useState } from "react";
import Link from "next/link";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { GitConnectionManager } from "@/components/workspace/GitConnectionManager";
import { LabelManager } from "@/components/workspace/LabelManager";
import { WorkflowStates } from "@/components/workspace/WorkflowStates";
import { WorkspaceInvite } from "@/components/workspace/WorkspaceInvite";
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

      {canManage && (
        <section className="mt-6 rounded-lg bg-white p-6 shadow-sm">
          <h2 className="mb-1 text-base font-semibold text-gray-900">メンバーを招待</h2>
          <p className="mb-4 text-sm text-gray-500">
            メールアドレスとロールを指定して招待します。受け取った人はリンクから参加できます。
          </p>
          <WorkspaceInvite workspaceId={workspaceId} />
        </section>
      )}

      <section className="mt-6 rounded-lg bg-white p-6 shadow-sm">
        <h2 className="mb-1 text-base font-semibold text-gray-900">ワークフロー(タスクの状態)</h2>
        <p className="mb-4 text-sm text-gray-500">
          カンバンの列になるタスクの状態です。{canManage ? "追加・並べ替え・削除ができます。" : "変更はワークスペース管理者(ADMIN)のみ可能です。"}
        </p>
        <WorkflowStates workspaceId={workspaceId} canManage={canManage} />
      </section>

      <section className="mt-6 rounded-lg bg-white p-6 shadow-sm">
        <h2 className="mb-1 text-base font-semibold text-gray-900">ラベル</h2>
        <p className="mb-4 text-sm text-gray-500">
          タスクに付けるラベルです。{canManage ? "追加・削除ができます。" : "変更はワークスペース管理者(ADMIN)のみ可能です。"}
        </p>
        <LabelManager workspaceId={workspaceId} canManage={canManage} />
      </section>

      <section className="mt-6 rounded-lg bg-white p-6 shadow-sm">
        <h2 className="mb-1 text-base font-semibold text-gray-900">Git 連携</h2>
        <p className="mb-4 text-sm text-gray-500">
          GitHub / GitLab への接続です。{canManage ? "追加・削除ができます。資格情報はシークレットストアの参照のみを登録します。" : "変更はワークスペース管理者(ADMIN)のみ可能です。"}
        </p>
        <GitConnectionManager workspaceId={workspaceId} canManage={canManage} />
      </section>
    </div>
  );
}
