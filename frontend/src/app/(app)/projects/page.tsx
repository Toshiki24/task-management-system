"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { ProjectList } from "@/components/project/ProjectList";
import { apiFetch } from "@/lib/api";
import type { Project } from "@/types/project";

export default function ProjectsPage() {
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

      {error && <ErrorMessage message={error} />}
      {!error && projects === null && <Loading />}
      {projects && <ProjectList projects={projects} />}
    </div>
  );
}
