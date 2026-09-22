"use client";

import { useRouter } from "next/navigation";
import { ProjectForm } from "@/components/project/ProjectForm";
import { apiFetch } from "@/lib/api";
import type { Project, ProjectRequestBody } from "@/types/project";

const INITIAL_VALUE: ProjectRequestBody = {
  name: "",
  description: "",
  status: "ACTIVE",
  startDate: "",
  endDate: "",
};

export default function NewProjectPage() {
  const router = useRouter();

  async function handleCreate(value: ProjectRequestBody) {
    const created = await apiFetch<Project>("/projects", {
      method: "POST",
      body: JSON.stringify(value),
    });
    router.push(`/projects/${created.id}`);
  }

  return (
    <div className="mx-auto max-w-xl">
      <h1 className="mb-6 text-lg font-bold text-gray-900">プロジェクト登録</h1>
      <div className="rounded-lg bg-white p-6 shadow-sm">
        <ProjectForm
          initialValue={INITIAL_VALUE}
          submitLabel="登録"
          submittingLabel="登録中..."
          onSubmit={handleCreate}
          onCancel={() => router.push("/projects")}
        />
      </div>
    </div>
  );
}
