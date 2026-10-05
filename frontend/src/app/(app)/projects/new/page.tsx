"use client";

import { Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
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

function NewProjectForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const workspaceId = searchParams.get("workspaceId");

  async function handleCreate(value: ProjectRequestBody) {
    if (!workspaceId) {
      // ワークスペースが特定できない場合は一覧へ戻す(一覧からワークスペースを選んで作成する)
      router.push("/projects");
      return;
    }
    const created = await apiFetch<Project>(`/workspaces/${workspaceId}/projects`, {
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

export default function NewProjectPage() {
  return (
    <Suspense fallback={null}>
      <NewProjectForm />
    </Suspense>
  );
}
