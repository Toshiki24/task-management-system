"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { ProjectList } from "@/components/project/ProjectList";
import { apiFetch } from "@/lib/api";
import type { Project } from "@/types/project";
import type { Workspace } from "@/types/workspace";

export default function ProjectsPage() {
  const [workspaces, setWorkspaces] = useState<Workspace[] | null>(null);
  const [workspaceId, setWorkspaceId] = useState<number | null>(null);
  const [projects, setProjects] = useState<Project[] | null>(null);
  const [error, setError] = useState<string | null>(null);

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
        所属しているワークスペースがありません。管理者に招待を依頼してください。
      </div>
    );
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <h1 className="text-lg font-bold text-gray-900">プロジェクト一覧</h1>
          {workspaces.length > 1 && (
            <select
              aria-label="ワークスペース"
              value={workspaceId ?? ""}
              onChange={(event) => setWorkspaceId(Number(event.target.value))}
              className="rounded-md border border-gray-300 px-2 py-1 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            >
              {workspaces.map((workspace) => (
                <option key={workspace.id} value={workspace.id}>
                  {workspace.name}
                </option>
              ))}
            </select>
          )}
        </div>
        {workspaceId !== null && (
          <Link
            href={`/projects/new?workspaceId=${workspaceId}`}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700"
          >
            ＋ 新規作成
          </Link>
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
