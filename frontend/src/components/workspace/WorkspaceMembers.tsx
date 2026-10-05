"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { Select } from "@/components/common/Select";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import type {
  AddWorkspaceMemberBody,
  UpdateWorkspaceMemberRoleBody,
  WorkspaceMember,
  WorkspaceRole,
} from "@/types/workspace";
import type { User } from "@/types/user";

const ROLE_OPTIONS: { value: WorkspaceRole; label: string }[] = [
  { value: "ADMIN", label: "ADMIN" },
  { value: "MEMBER", label: "MEMBER" },
  { value: "VIEWER", label: "VIEWER" },
];

interface WorkspaceMembersProps {
  workspaceId: number;
  /** 呼び出しユーザーが管理操作(追加・ロール変更・削除)を行えるか(WS Admin 以上) */
  canManage: boolean;
}

export function WorkspaceMembers({ workspaceId, canManage }: WorkspaceMembersProps) {
  const [members, setMembers] = useState<WorkspaceMember[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [isAdding, setIsAdding] = useState(false);
  const [availableUsers, setAvailableUsers] = useState<User[] | null>(null);
  const [newUserId, setNewUserId] = useState("");
  const [newRole, setNewRole] = useState<WorkspaceRole>("MEMBER");
  const [isAddSubmitting, setIsAddSubmitting] = useState(false);

  const [removeTarget, setRemoveTarget] = useState<WorkspaceMember | null>(null);
  const [isRemoving, setIsRemoving] = useState(false);

  useEffect(() => {
    apiFetch<WorkspaceMember[]>(`/workspaces/${workspaceId}/members`)
      .then(setMembers)
      .catch(() => setError("メンバー情報の取得に失敗しました。"));
  }, [workspaceId]);

  async function reloadMembers() {
    setMembers(await apiFetch<WorkspaceMember[]>(`/workspaces/${workspaceId}/members`));
  }

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

    setIsAddSubmitting(true);
    try {
      const body: AddWorkspaceMemberBody = { userId: Number(newUserId), role: newRole };
      await apiFetch(`/workspaces/${workspaceId}/members`, {
        method: "POST",
        body: JSON.stringify(body),
      });
      await reloadMembers();
      setIsAdding(false);
      setNewUserId("");
      setNewRole("MEMBER");
    } catch (err) {
      setError(formatApiErrorMessage(err, "メンバーの追加に失敗しました。"));
    } finally {
      setIsAddSubmitting(false);
    }
  }

  async function handleRoleChange(member: WorkspaceMember, role: WorkspaceRole) {
    setError(null);
    try {
      const body: UpdateWorkspaceMemberRoleBody = { role };
      await apiFetch(`/workspaces/${workspaceId}/members/${member.userId}`, {
        method: "PATCH",
        body: JSON.stringify(body),
      });
      await reloadMembers();
    } catch (err) {
      setError(formatApiErrorMessage(err, "ロールの変更に失敗しました。"));
      await reloadMembers();
    }
  }

  async function handleRemoveConfirmed() {
    if (!removeTarget) return;
    setIsRemoving(true);
    try {
      await apiFetch<void>(`/workspaces/${workspaceId}/members/${removeTarget.userId}`, {
        method: "DELETE",
      });
      setMembers((current) => current?.filter((m) => m.userId !== removeTarget.userId) ?? null);
    } catch (err) {
      setError(formatApiErrorMessage(err, "メンバーの削除に失敗しました。"));
    } finally {
      setIsRemoving(false);
      setRemoveTarget(null);
    }
  }

  const candidateUsers =
    availableUsers?.filter((user) => !members?.some((m) => m.userId === user.id)) ?? [];

  return (
    <div>
      {error && <ErrorMessage message={error} />}
      {!members && !error && <Loading />}

      {members && members.length > 0 && (
        <ul className="mb-4 divide-y divide-gray-100">
          {members.map((member) => (
            <li key={member.userId} className="flex items-center justify-between gap-3 py-2 text-sm">
              <span className="text-gray-900">
                {member.name} <span className="text-gray-500">({member.email})</span>
              </span>
              <span className="flex items-center gap-3">
                {canManage ? (
                  <Select
                    id={`role-${member.userId}`}
                    aria-label={`${member.name} のロール`}
                    value={member.role}
                    onChange={(event) => handleRoleChange(member, event.target.value as WorkspaceRole)}
                    options={ROLE_OPTIONS}
                  />
                ) : (
                  <span className="text-gray-500">{member.role}</span>
                )}
                {canManage && (
                  <button
                    type="button"
                    onClick={() => setRemoveTarget(member)}
                    className="text-red-600 hover:underline"
                  >
                    削除
                  </button>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}

      {members && members.length === 0 && (
        <p className="mb-4 text-sm text-gray-500">メンバーがいません。</p>
      )}

      {canManage && !isAdding && (
        <Button type="button" variant="secondary" onClick={openAddForm}>
          メンバー追加
        </Button>
      )}

      {canManage && isAdding && (
        <form
          onSubmit={handleAdd}
          className="flex flex-wrap items-end gap-3 rounded-md border border-gray-200 p-4"
        >
          <Select
            id="newWorkspaceMemberUserId"
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
            id="newWorkspaceMemberRole"
            label="ロール"
            value={newRole}
            onChange={(event) => setNewRole(event.target.value as WorkspaceRole)}
            options={ROLE_OPTIONS}
          />
          <Button type="button" variant="secondary" onClick={() => setIsAdding(false)} disabled={isAddSubmitting}>
            キャンセル
          </Button>
          <Button type="submit" variant="primary" disabled={isAddSubmitting}>
            {isAddSubmitting ? "追加中..." : "追加"}
          </Button>
        </form>
      )}

      <Modal isOpen={removeTarget !== null} title="メンバー削除" onClose={() => setRemoveTarget(null)}>
        <p>{removeTarget?.name} をこのワークスペースから削除しますか？</p>
        <div className="mt-6 flex justify-end gap-3">
          <Button type="button" variant="secondary" onClick={() => setRemoveTarget(null)} disabled={isRemoving}>
            キャンセル
          </Button>
          <Button type="button" variant="danger" onClick={handleRemoveConfirmed} disabled={isRemoving}>
            {isRemoving ? "削除中..." : "削除"}
          </Button>
        </div>
      </Modal>
    </div>
  );
}
