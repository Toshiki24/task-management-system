"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { Select } from "@/components/common/Select";
import { ApiError, apiFetch } from "@/lib/api";
import type {
  AddMemberRequestBody,
  Member,
  ProjectMemberRole,
} from "@/types/member";
import type { User } from "@/types/user";

const ROLE_OPTIONS: { value: ProjectMemberRole; label: string }[] = [
  { value: "OWNER", label: "OWNER" },
  { value: "MEMBER", label: "MEMBER" },
];

interface ProjectMemberListProps {
  projectId: string;
}

export function ProjectMemberList({ projectId }: ProjectMemberListProps) {
  const [members, setMembers] = useState<Member[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [isAdding, setIsAdding] = useState(false);
  const [availableUsers, setAvailableUsers] = useState<User[] | null>(null);
  const [newUserId, setNewUserId] = useState("");
  const [newRole, setNewRole] = useState<ProjectMemberRole>("MEMBER");

  const [removeTarget, setRemoveTarget] = useState<Member | null>(null);

  useEffect(() => {
    apiFetch<Member[]>(`/projects/${projectId}/members`)
      .then(setMembers)
      .catch(() => setError("メンバー情報の取得に失敗しました。"));
  }, [projectId]);

  async function openAddForm() {
    setIsAdding(true);
    setError(null);
    if (!availableUsers) {
      try {
        setAvailableUsers(await apiFetch<User[]>("/users"));
      } catch {
        setError("ユーザー情報の取得に失敗しました。");
      }
    }
  }

  async function handleAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (!newUserId) {
      setError("追加するユーザーを選択してください。");
      return;
    }

    try {
      const body: AddMemberRequestBody = {
        userId: Number(newUserId),
        role: newRole,
      };
      await apiFetch(`/projects/${projectId}/members`, {
        method: "POST",
        body: JSON.stringify(body),
      });

      setMembers(await apiFetch<Member[]>(`/projects/${projectId}/members`));
      setIsAdding(false);
      setNewUserId("");
      setNewRole("MEMBER");
    } catch (err) {
      setError(
        err instanceof ApiError ? err.message : "メンバーの追加に失敗しました。",
      );
    }
  }

  async function handleRemoveConfirmed() {
    if (!removeTarget) return;

    try {
      await apiFetch<void>(
        `/projects/${projectId}/members/${removeTarget.userId}`,
        { method: "DELETE" },
      );
      setMembers(
        (current) =>
          current?.filter((member) => member.userId !== removeTarget.userId) ??
          null,
      );
    } catch {
      setError("メンバーの削除に失敗しました。");
    } finally {
      setRemoveTarget(null);
    }
  }

  const candidateUsers =
    availableUsers?.filter(
      (user) => !members?.some((member) => member.userId === user.id),
    ) ?? [];

  return (
    <div>
      {error && <ErrorMessage message={error} />}

      {!members && !error && <Loading />}

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
                onClick={() => setRemoveTarget(member)}
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

      {!isAdding && (
        <Button type="button" variant="secondary" onClick={openAddForm}>
          メンバー追加
        </Button>
      )}

      {isAdding && (
        <form
          onSubmit={handleAdd}
          className="flex flex-wrap items-end gap-3 rounded-md border border-gray-200 p-4"
        >
          <Select
            id="newMemberUserId"
            label="ユーザー"
            value={newUserId}
            onChange={(event) => setNewUserId(event.target.value)}
            options={[
              { value: "", label: "選択してください" },
              ...candidateUsers.map((user) => ({
                value: String(user.id),
                label: `${user.name} (${user.email})`,
              })),
            ]}
          />
          <Select
            id="newMemberRole"
            label="ロール"
            value={newRole}
            onChange={(event) =>
              setNewRole(event.target.value as ProjectMemberRole)
            }
            options={ROLE_OPTIONS}
          />
          <Button
            type="button"
            variant="secondary"
            onClick={() => setIsAdding(false)}
          >
            キャンセル
          </Button>
          <Button type="submit" variant="primary">
            追加
          </Button>
        </form>
      )}

      <Modal
        isOpen={removeTarget !== null}
        title="メンバー削除"
        onClose={() => setRemoveTarget(null)}
      >
        <p>{removeTarget?.name} をこのプロジェクトから削除しますか？</p>
        <div className="mt-6 flex justify-end gap-3">
          <Button
            type="button"
            variant="secondary"
            onClick={() => setRemoveTarget(null)}
          >
            キャンセル
          </Button>
          <Button type="button" variant="danger" onClick={handleRemoveConfirmed}>
            削除
          </Button>
        </div>
      </Modal>
    </div>
  );
}
