"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { Select } from "@/components/common/Select";
import { ProjectList } from "@/components/project/ProjectList";
import { apiFetch } from "@/lib/api";
import { fetchCurrentUser } from "@/lib/auth";
import type { Project } from "@/types/project";
import type { Workspace } from "@/types/workspace";

export default function ProjectsPage() {
  const [workspaces, setWorkspaces] = useState<Workspace[] | null>(null);
  const [workspaceId, setWorkspaceId] = useState<number | null>(null);
  const [projects, setProjects] = useState<Project[] | null>(null);
  const [isSystemAdmin, setIsSystemAdmin] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // System Admin だけに表示する操作(ワークスペース作成)の判定用
  useEffect(() => {
    fetchCurrentUser()
      .then((user) => setIsSystemAdmin(user?.isSystemAdmin ?? false))
      .catch(() => setIsSystemAdmin(false));
  }, []);

  // 自分が所属するワークスペースを取得し、先頭を初期選択にする
  useEffect(() => {
    apiFetch<Workspace[]>("/me/workspaces")
      .then((data) => {
        setWorkspaces(data);
        if (data.length > 0) {
          setWorkspaceId(data[0].id);
        }
      })
      .catch(() => setError("ワークスペース情報の取得に失敗しました。"));
  }, []);

  // 選択中のワークスペースのプロジェクトを取得する
  useEffect(() => {
    if (workspaceId === null) {
      return;
    }
    setProjects(null);
    apiFetch<Project[]>(`/workspaces/${workspaceId}/projects`)
      .then(setProjects)
      .catch(() => setError("プロジェクト情報の取得に失敗しました。"));
  }, [workspaceId]);

  if (error) {
    return <ErrorMessage message={error} />;
  }

  if (workspaces === null) {
    return <Loading />;
  }

  if (workspaces.length === 0) {
    return (
      <div className="text-sm text-gray-500">
        <p className="mb-2">所属しているワークスペースがありません。管理者に招待を依頼してください。</p>
        {isSystemAdmin && (
          <Link href="/workspaces/new" className="font-semibold text-blue-600 hover:underline">
            ＋ ワークスペースを作成
          </Link>
        )}
      </div>
    );
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <h1 className="text-lg font-bold text-gray-900">プロジェクト一覧</h1>
          {workspaces.length > 1 && (
            <Select
              aria-label="ワークスペース"
              value={workspaceId !== null ? String(workspaceId) : ""}
              onChange={(event) => setWorkspaceId(Number(event.target.value))}
              options={workspaces.map((workspace) => ({
                value: String(workspace.id),
                label: workspace.name,
              }))}
            />
          )}
        </div>
        {workspaceId !== null && (
          <div className="flex items-center gap-3">
            {isSystemAdmin && (
              <Link
                href="/workspaces/new"
                className="text-sm font-semibold text-blue-600 hover:underline"
              >
                ＋ ワークスペース
              </Link>
            )}
            <Link
              href={`/workspaces/${workspaceId}`}
              className="text-sm font-semibold text-blue-600 hover:underline"
            >
              ワークスペース設定
            </Link>
            <Link
              href={`/projects/new?workspaceId=${workspaceId}`}
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700"
            >
              ＋ 新規作成
            </Link>
          </div>
        )}
      </div>

      {projects === null ? (
        <Loading />
      ) : (
        <ProjectList projects={projects} workspaceId={workspaceId} />
      )}
    </div>
  );
}
