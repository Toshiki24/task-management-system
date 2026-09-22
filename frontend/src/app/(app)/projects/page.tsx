"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { apiFetch } from "@/lib/api";
import type { Project } from "@/types/project";

export default function ProjectsPage() {
  const router = useRouter();
  const [projects, setProjects] = useState<Project[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<Project[]>("/projects")
      .then(setProjects)
      .catch(() => setError("プロジェクト情報の取得に失敗しました。"));
  }, []);

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-lg font-bold text-gray-900">プロジェクト一覧</h1>
        <Link
          href="/projects/new"
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700"
        >
          ＋ 新規作成
        </Link>
      </div>

      {error && <p className="text-sm text-red-600">{error}</p>}

      {!error && projects === null && (
        <p className="text-sm text-gray-500">読み込み中...</p>
      )}

      {projects && projects.length === 0 && (
        <p className="text-sm text-gray-500">プロジェクトがありません。</p>
      )}

      {projects && projects.length > 0 && (
        <table className="w-full border-collapse overflow-hidden rounded-lg bg-white text-sm shadow-sm">
          <thead>
            <tr className="border-b border-gray-200 text-left text-gray-500">
              <th className="px-4 py-2 font-medium">プロジェクト名</th>
              <th className="px-4 py-2 font-medium">ステータス</th>
              <th className="px-4 py-2 font-medium">開始日</th>
            </tr>
          </thead>
          <tbody>
            {projects.map((project) => (
              <tr
                key={project.id}
                onClick={() => router.push(`/projects/${project.id}`)}
                className="cursor-pointer border-b border-gray-100 last:border-0 hover:bg-gray-50"
              >
                <td className="px-4 py-3 text-gray-900">{project.name}</td>
                <td className="px-4 py-3 text-gray-600">{project.status}</td>
                <td className="px-4 py-3 text-gray-600">
                  {project.startDate ?? "-"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
