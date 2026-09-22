"use client";

import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { apiFetch, ApiError } from "@/lib/api";
import type { Project, ProjectRequestBody, ProjectStatus } from "@/types/project";
import type { AddMemberRequestBody, Member, ProjectMemberRole } from "@/types/member";
import type { User } from "@/types/user";

const STATUS_OPTIONS: ProjectStatus[] = ["ACTIVE", "COMPLETED", "ARCHIVED"];
const ROLE_OPTIONS: ProjectMemberRole[] = ["OWNER", "MEMBER"];

export default function ProjectDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const projectId = params.id;

  const [project, setProject] = useState<Project | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [isEditing, setIsEditing] = useState(false);
  const [editForm, setEditForm] = useState<ProjectRequestBody | null>(null);
  const [editError, setEditError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const [members, setMembers] = useState<Member[] | null>(null);
  const [memberError, setMemberError] = useState<string | null>(null);
  const [isAddingMember, setIsAddingMember] = useState(false);
  const [availableUsers, setAvailableUsers] = useState<User[] | null>(null);
  const [newMemberUserId, setNewMemberUserId] = useState("");
  const [newMemberRole, setNewMemberRole] = useState<ProjectMemberRole>("MEMBER");

  useEffect(() => {
    apiFetch<Project>(`/projects/${projectId}`)
      .then(setProject)
      .catch(() => setLoadError("プロジェクト情報の取得に失敗しました。"));

    apiFetch<Member[]>(`/projects/${projectId}/members`)
      .then(setMembers)
      .catch(() => setMemberError("メンバー情報の取得に失敗しました。"));
  }, [projectId]);

  function startEditing() {
    if (!project) return;
    setEditForm({
      name: project.name,
      description: project.description,
      status: project.status,
      startDate: project.startDate,
      endDate: project.endDate,
    });
    setEditError(null);
    setIsEditing(true);
  }

  async function handleUpdate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editForm) return;
    setEditError(null);

    if (
      editForm.startDate &&
      editForm.endDate &&
      editForm.endDate < editForm.startDate
    ) {
      setEditError("終了日は開始日以降の日付を指定してください。");
      return;
    }

    setIsSaving(true);
    try {
      const updated = await apiFetch<Project>(`/projects/${projectId}`, {
        method: "PUT",
        body: JSON.stringify(editForm),
      });
      setProject(updated);
      setIsEditing(false);
    } catch (err) {
      setEditError(
        err instanceof ApiError ? err.message : "プロジェクトの更新に失敗しました。",
      );
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDelete() {
    const confirmed = window.confirm(
      "このプロジェクトを削除しますか？\n\nプロジェクトに紐付くタスクやコメントも削除されます。",
    );
    if (!confirmed) return;

    try {
      await apiFetch<void>(`/projects/${projectId}`, { method: "DELETE" });
      router.push("/projects");
    } catch {
      window.alert("プロジェクトの削除に失敗しました。");
    }
  }

  async function openAddMemberForm() {
    setIsAddingMember(true);
    setMemberError(null);
    if (!availableUsers) {
      try {
        setAvailableUsers(await apiFetch<User[]>("/users"));
      } catch {
        setMemberError("ユーザー情報の取得に失敗しました。");
      }
    }
  }

  async function handleAddMember(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMemberError(null);

    if (!newMemberUserId) {
      setMemberError("追加するユーザーを選択してください。");
      return;
    }

    try {
      const body: AddMemberRequestBody = {
        userId: Number(newMemberUserId),
        role: newMemberRole,
      };
      await apiFetch(`/projects/${projectId}/members`, {
        method: "POST",
        body: JSON.stringify(body),
      });

      const refreshed = await apiFetch<Member[]>(`/projects/${projectId}/members`);
      setMembers(refreshed);
      setIsAddingMember(false);
      setNewMemberUserId("");
      setNewMemberRole("MEMBER");
    } catch (err) {
      setMemberError(
        err instanceof ApiError ? err.message : "メンバーの追加に失敗しました。",
      );
    }
  }

  async function handleRemoveMember(userId: number) {
    const confirmed = window.confirm("このメンバーを削除しますか？");
    if (!confirmed) return;

    try {
      await apiFetch<void>(`/projects/${projectId}/members/${userId}`, {
        method: "DELETE",
      });
      setMembers((current) => current?.filter((m) => m.userId !== userId) ?? null);
    } catch {
      window.alert("メンバーの削除に失敗しました。");
    }
  }

  if (loadError) {
    return <p className="text-sm text-red-600">{loadError}</p>;
  }

  if (!project) {
    return <p className="text-sm text-gray-500">読み込み中...</p>;
  }

  const candidateUsers =
    availableUsers?.filter(
      (user) => !members?.some((member) => member.userId === user.id),
    ) ?? [];

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="rounded-lg bg-white p-6 shadow-sm">
        <div className="mb-4 flex items-center justify-between">
          <h1 className="text-lg font-bold text-gray-900">プロジェクト詳細</h1>
          {!isEditing && (
            <div className="flex gap-2">
              <button
                type="button"
                onClick={startEditing}
                className="rounded-md border border-gray-300 px-3 py-1.5 text-sm font-semibold text-gray-700 hover:bg-gray-50"
              >
                編集
              </button>
              <button
                type="button"
                onClick={handleDelete}
                className="rounded-md border border-red-300 px-3 py-1.5 text-sm font-semibold text-red-600 hover:bg-red-50"
              >
                削除
              </button>
            </div>
          )}
        </div>

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
                  {project.startDate ?? "-"}
                </dd>
              </div>
              <div>
                <dt className="text-gray-500">終了日</dt>
                <dd className="mt-0.5 text-gray-900">{project.endDate ?? "-"}</dd>
              </div>
            </div>
          </dl>
        )}

        {isEditing && editForm && (
          <form onSubmit={handleUpdate} className="space-y-4">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                プロジェクト名 <span className="text-red-500">*</span>
              </label>
              <input
                required
                maxLength={200}
                value={editForm.name}
                onChange={(e) =>
                  setEditForm({ ...editForm, name: e.target.value })
                }
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
              />
            </div>

            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                説明
              </label>
              <textarea
                rows={3}
                value={editForm.description ?? ""}
                onChange={(e) =>
                  setEditForm({ ...editForm, description: e.target.value })
                }
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
              />
            </div>

            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                ステータス
              </label>
              <select
                value={editForm.status}
                onChange={(e) =>
                  setEditForm({
                    ...editForm,
                    status: e.target.value as ProjectStatus,
                  })
                }
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
              >
                {STATUS_OPTIONS.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>

            <div className="flex gap-4">
              <div className="flex-1">
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  開始日
                </label>
                <input
                  type="date"
                  value={editForm.startDate ?? ""}
                  onChange={(e) =>
                    setEditForm({ ...editForm, startDate: e.target.value })
                  }
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
                />
              </div>
              <div className="flex-1">
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  終了日
                </label>
                <input
                  type="date"
                  value={editForm.endDate ?? ""}
                  onChange={(e) =>
                    setEditForm({ ...editForm, endDate: e.target.value })
                  }
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
                />
              </div>
            </div>

            {editError && <p className="text-sm text-red-600">{editError}</p>}

            <div className="flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setIsEditing(false)}
                className="rounded-md border border-gray-300 px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50"
              >
                キャンセル
              </button>
              <button
                type="submit"
                disabled={isSaving}
                className="rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
              >
                {isSaving ? "保存中..." : "保存"}
              </button>
            </div>
          </form>
        )}
      </div>

      <div className="rounded-lg bg-white p-6 shadow-sm">
        <h2 className="mb-4 text-base font-bold text-gray-900">メンバー</h2>

        {memberError && (
          <p className="mb-3 text-sm text-red-600">{memberError}</p>
        )}

        {!members && !memberError && (
          <p className="text-sm text-gray-500">読み込み中...</p>
        )}

        {members && members.length > 0 && (
          <ul className="mb-4 divide-y divide-gray-100">
            {members.map((member) => (
              <li
                key={member.userId}
                className="flex items-center justify-between py-2 text-sm"
              >
                <span className="text-gray-900">
                  {member.name}{" "}
                  <span className="text-gray-500">({member.role})</span>
                </span>
                <button
                  type="button"
                  onClick={() => handleRemoveMember(member.userId)}
                  className="text-red-600 hover:underline"
                >
                  削除
                </button>
              </li>
            ))}
          </ul>
        )}

        {members && members.length === 0 && (
          <p className="mb-4 text-sm text-gray-500">メンバーがいません。</p>
        )}

        {!isAddingMember && (
          <button
            type="button"
            onClick={openAddMemberForm}
            className="rounded-md border border-gray-300 px-3 py-1.5 text-sm font-semibold text-gray-700 hover:bg-gray-50"
          >
            メンバー追加
          </button>
        )}

        {isAddingMember && (
          <form
            onSubmit={handleAddMember}
            className="flex flex-wrap items-end gap-3 rounded-md border border-gray-200 p-4"
          >
            <div>
              <label className="mb-1 block text-xs font-medium text-gray-700">
                ユーザー
              </label>
              <select
                value={newMemberUserId}
                onChange={(e) => setNewMemberUserId(e.target.value)}
                className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
              >
                <option value="">選択してください</option>
                {candidateUsers.map((user) => (
                  <option key={user.id} value={user.id}>
                    {user.name} ({user.email})
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-gray-700">
                ロール
              </label>
              <select
                value={newMemberRole}
                onChange={(e) =>
                  setNewMemberRole(e.target.value as ProjectMemberRole)
                }
                className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
              >
                {ROLE_OPTIONS.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>

            <button
              type="button"
              onClick={() => setIsAddingMember(false)}
              className="rounded-md border border-gray-300 px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50"
            >
              キャンセル
            </button>
            <button
              type="submit"
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700"
            >
              追加
            </button>
          </form>
        )}
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
    </div>
  );
}
