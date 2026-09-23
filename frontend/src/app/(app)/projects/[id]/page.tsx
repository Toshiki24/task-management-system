"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { ProjectForm } from "@/components/project/ProjectForm";
import { ProjectMemberList } from "@/components/project/ProjectMemberList";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { formatDate } from "@/lib/format";
import type { Project } from "@/types/project";

export default function ProjectDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const projectId = params.id;

  const [project, setProject] = useState<Project | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  useEffect(() => {
    apiFetch<Project>(`/projects/${projectId}`)
      .then(setProject)
      .catch(() => setLoadError("プロジェクト情報の取得に失敗しました。"));
  }, [projectId]);

  async function handleDelete() {
    setIsDeleting(true);
    try {
      await apiFetch<void>(`/projects/${projectId}`, { method: "DELETE" });
      router.push("/projects");
    } catch (err) {
      setDeleteError(formatApiErrorMessage(err, "プロジェクトの削除に失敗しました。"));
      setIsDeleteModalOpen(false);
      setIsDeleting(false);
    }
  }

  if (loadError) {
    return <ErrorMessage message={loadError} />;
  }

  if (!project) {
    return <Loading />;
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="rounded-lg bg-white p-6 shadow-sm">
        <div className="mb-4 flex items-center justify-between">
          <h1 className="text-lg font-bold text-gray-900">プロジェクト詳細</h1>
          {!isEditing && (
            <div className="flex gap-2">
              <Button
                type="button"
                variant="secondary"
                onClick={() => setIsEditing(true)}
              >
                編集
              </Button>
              <Button
                type="button"
                variant="danger"
                onClick={() => setIsDeleteModalOpen(true)}
              >
                削除
              </Button>
            </div>
          )}
        </div>

        {deleteError && <ErrorMessage message={deleteError} />}

        {!isEditing && (
          <dl className="space-y-4 text-sm">
            <div>
              <dt className="text-gray-500">プロジェクト名</dt>
              <dd className="mt-0.5 text-gray-900">{project.name}</dd>
            </div>
            <div>
              <dt className="text-gray-500">ステータス</dt>
              <dd className="mt-0.5 text-gray-900">{project.status}</dd>
            </div>
            <div>
              <dt className="text-gray-500">説明</dt>
              <dd className="mt-0.5 whitespace-pre-wrap text-gray-900">
                {project.description ?? "-"}
              </dd>
            </div>
            <div className="flex gap-8">
              <div>
                <dt className="text-gray-500">開始日</dt>
                <dd className="mt-0.5 text-gray-900">
                  {formatDate(project.startDate)}
                </dd>
              </div>
              <div>
                <dt className="text-gray-500">終了日</dt>
                <dd className="mt-0.5 text-gray-900">
                  {formatDate(project.endDate)}
                </dd>
              </div>
            </div>
          </dl>
        )}

        {isEditing && (
          <ProjectForm
            initialValue={{
              name: project.name,
              description: project.description,
              status: project.status,
              startDate: project.startDate,
              endDate: project.endDate,
            }}
            submitLabel="保存"
            submittingLabel="保存中..."
            onSubmit={async (value) => {
              const updated = await apiFetch<Project>(`/projects/${projectId}`, {
                method: "PUT",
                body: JSON.stringify(value),
              });
              setProject(updated);
              setIsEditing(false);
            }}
            onCancel={() => setIsEditing(false)}
          />
        )}
      </div>

      <div className="rounded-lg bg-white p-6 shadow-sm">
        <h2 className="mb-4 text-base font-bold text-gray-900">メンバー</h2>
        <ProjectMemberList projectId={projectId} />
      </div>

      <div className="rounded-lg bg-white p-6 shadow-sm">
        <h2 className="mb-4 text-base font-bold text-gray-900">タスク</h2>
        <Link
          href={`/projects/${projectId}/tasks`}
          className="text-sm font-semibold text-blue-600 hover:underline"
        >
          タスク一覧を見る
        </Link>
      </div>

      <Modal
        isOpen={isDeleteModalOpen}
        title="プロジェクト削除"
        onClose={() => setIsDeleteModalOpen(false)}
      >
        <p>
          このプロジェクトを削除しますか？
          <br />
          プロジェクトに紐付くタスクやコメントも削除されます。
        </p>
        <div className="mt-6 flex justify-end gap-3">
          <Button
            type="button"
            variant="secondary"
            onClick={() => setIsDeleteModalOpen(false)}
            disabled={isDeleting}
          >
            キャンセル
          </Button>
          <Button
            type="button"
            variant="danger"
            onClick={handleDelete}
            disabled={isDeleting}
          >
            {isDeleting ? "削除中..." : "削除"}
          </Button>
        </div>
      </Modal>
    </div>
  );
}
