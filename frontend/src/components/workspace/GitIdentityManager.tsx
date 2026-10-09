"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Input } from "@/components/common/Input";
import { Modal } from "@/components/common/Modal";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { GIT_PROVIDER_LABELS, type GitIdentity, type GitIdentityRequestBody, type GitProvider } from "@/types/git";
import type { WorkspaceMember } from "@/types/workspace";

interface GitIdentityManagerProps {
  workspaceId: number;
  /** 呼び出しユーザーが対応付けを管理できるか(WS Admin) */
  canManage: boolean;
}

export function GitIdentityManager({ workspaceId, canManage }: GitIdentityManagerProps) {
  const [identities, setIdentities] = useState<GitIdentity[] | null>(null);
  const [members, setMembers] = useState<WorkspaceMember[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [isAdding, setIsAdding] = useState(false);
  const [userId, setUserId] = useState("");
  const [provider, setProvider] = useState<GitProvider>("GITHUB");
  const [externalUserId, setExternalUserId] = useState("");
  const [externalUsername, setExternalUsername] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState<GitIdentity | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  useEffect(() => {
    if (!canManage) {
      setIdentities([]);
      return;
    }
    apiFetch<GitIdentity[]>(`/workspaces/${workspaceId}/git-identities`)
      .then(setIdentities)
      .catch(() => setError("Git ユーザー対応付けの取得に失敗しました。"));
    apiFetch<WorkspaceMember[]>(`/workspaces/${workspaceId}/members`)
      .then(setMembers)
      .catch(() => {
        // メンバー候補が取れなくても一覧表示は行う
      });
  }, [workspaceId, canManage]);

  async function reload() {
    setIdentities(await apiFetch<GitIdentity[]>(`/workspaces/${workspaceId}/git-identities`));
  }

  async function handleAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    if (!userId) {
      setError("対象メンバーを選択してください。");
      return;
    }
    if (!externalUserId.trim()) {
      setError("Git ユーザーIDを入力してください。");
      return;
    }
    setIsSubmitting(true);
    try {
      const body: GitIdentityRequestBody = {
        userId: Number(userId),
        provider,
        externalUserId: externalUserId.trim(),
        externalUsername: externalUsername.trim() || null,
      };
      await apiFetch(`/workspaces/${workspaceId}/git-identities`, { method: "POST", body: JSON.stringify(body) });
      await reload();
      setIsAdding(false);
      setUserId("");
      setExternalUserId("");
      setExternalUsername("");
    } catch (err) {
      setError(formatApiErrorMessage(err, "Git ユーザー対応付けの追加に失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDeleteConfirmed() {
    if (!deleteTarget) return;
    setIsDeleting(true);
    setError(null);
    try {
      await apiFetch<void>(`/git-identities/${deleteTarget.id}`, { method: "DELETE" });
      await reload();
      setDeleteTarget(null);
    } catch (err) {
      setError(formatApiErrorMessage(err, "Git ユーザー対応付けの削除に失敗しました。"));
    } finally {
      setIsDeleting(false);
    }
  }

  if (!canManage) {
    return <p className="text-sm text-gray-500">Git ユーザーの対応付けはワークスペース管理者(ADMIN)のみ可能です。</p>;
  }

  if (!identities && !error) {
    return <p className="text-sm text-gray-500">読み込み中...</p>;
  }

  return (
    <div>
      {error && <ErrorMessage message={error} />}

      {identities && identities.length > 0 && (
        <ul className="mb-4 divide-y divide-gray-100 rounded-md border border-gray-200">
          {identities.map((identity) => (
            <li key={identity.id} className="flex items-center justify-between gap-2 px-3 py-2 text-sm">
              <div className="min-w-0">
                <span className="font-medium text-gray-900">{identity.userName}</span>
                <span className="mx-2 text-gray-400">↔</span>
                <span className="text-gray-700">{GIT_PROVIDER_LABELS[identity.provider]}</span>
                <span className="ml-1 text-gray-600">
                  {identity.externalUsername ?? identity.externalUserId}
                </span>
              </div>
              <button
                type="button"
                aria-label={`${identity.userName} の対応付けを削除`}
                onClick={() => setDeleteTarget(identity)}
                className="shrink-0 text-red-600 hover:underline"
              >
                削除
              </button>
            </li>
          ))}
        </ul>
      )}

      {identities && identities.length === 0 && (
        <p className="mb-4 text-sm text-gray-500">対応付けがありません。</p>
      )}

      {!isAdding && (
        <Button type="button" variant="secondary" onClick={() => setIsAdding(true)}>
          対応付けを追加
        </Button>
      )}

      {isAdding && (
        <form onSubmit={handleAdd} className="space-y-3 rounded-md border border-gray-200 p-4">
          <div>
            <label htmlFor="identityMember" className="mb-1 block text-sm font-medium text-gray-700">メンバー</label>
            <select
              id="identityMember"
              value={userId}
              onChange={(e) => setUserId(e.target.value)}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
            >
              <option value="">選択してください</option>
              {members.map((m) => (
                <option key={m.userId} value={m.userId}>
                  {m.name}（{m.email}）
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="identityProvider" className="mb-1 block text-sm font-medium text-gray-700">プロバイダ</label>
            <select
              id="identityProvider"
              value={provider}
              onChange={(e) => setProvider(e.target.value as GitProvider)}
              className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
            >
              <option value="GITHUB">GitHub</option>
              <option value="GITLAB">GitLab</option>
            </select>
          </div>
          <Input
            id="identityExternalId"
            label="Git ユーザーID(プロバイダ側の安定ID)"
            value={externalUserId}
            onChange={(e) => setExternalUserId(e.target.value)}
            placeholder="123456"
          />
          <Input
            id="identityUsername"
            label="Git ユーザー名(任意・表示用)"
            value={externalUsername}
            onChange={(e) => setExternalUsername(e.target.value)}
            placeholder="octocat"
          />
          <div className="flex gap-3">
            <Button type="button" variant="secondary" onClick={() => setIsAdding(false)} disabled={isSubmitting}>
              キャンセル
            </Button>
            <Button type="submit" variant="primary" disabled={isSubmitting}>
              {isSubmitting ? "追加中..." : "追加"}
            </Button>
          </div>
        </form>
      )}

      <Modal isOpen={deleteTarget !== null} title="対応付けの削除" onClose={() => setDeleteTarget(null)}>
        <p>「{deleteTarget?.userName}」の Git ユーザー対応付けを削除しますか？</p>
        <div className="mt-6 flex justify-end gap-3">
          <Button type="button" variant="secondary" onClick={() => setDeleteTarget(null)} disabled={isDeleting}>
            キャンセル
          </Button>
          <Button type="button" variant="danger" onClick={handleDeleteConfirmed} disabled={isDeleting}>
            {isDeleting ? "削除中..." : "削除"}
          </Button>
        </div>
      </Modal>
    </div>
  );
}
