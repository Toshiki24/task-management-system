"use client";

import { useRouter } from "next/navigation";
import type { Project } from "@/types/project";

interface ProjectListProps {
  projects: Project[];
}

export function ProjectList({ projects }: ProjectListProps) {
  const router = useRouter();

  if (projects.length === 0) {
    return <p className="text-sm text-gray-500">プロジェクトがありません。</p>;
  }

  return (
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
  );
}
