"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Input } from "@/components/common/Input";
import { Modal } from "@/components/common/Modal";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import {
  GIT_AUTH_TYPE_LABELS,
  GIT_CONNECTION_STATUS_LABELS,
  GIT_PROVIDER_LABELS,
  type GitAuthType,
  type GitConnection,
  type GitConnectionRequestBody,
  type GitProvider,
} from "@/types/git";

interface GitConnectionManagerProps {
  workspaceId: number;
  /** 呼び出しユーザーが Git 接続を管理できるか(WS Admin) */
  canManage: boolean;
}

export function GitConnectionManager({ workspaceId, canManage }: GitConnectionManagerProps) {
  const [connections, setConnections] = useState<GitConnection[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [isAdding, setIsAdding] = useState(false);
  const [provider, setProvider] = useState<GitProvider>("GITHUB");
  const [authType, setAuthType] = useState<GitAuthType>("GITHUB_APP");
  const [account, setAccount] = useState("");
  const [baseUrl, setBaseUrl] = useState("");
  const [secretRef, setSecretRef] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState<GitConnection | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  useEffect(() => {
    // 接続は WS Admin のみ参照できる。権限がなければ取得しない
    if (!canManage) {
      setConnections([]);
      return;
    }
    apiFetch<GitConnection[]>(`/workspaces/${workspaceId}/git-connections`)
      .then(setConnections)
      .catch(() => setError("Git 接続の取得に失敗しました。"));
  }, [workspaceId, canManage]);

  async function reload() {
    setConnections(await apiFetch<GitConnection[]>(`/workspaces/${workspaceId}/git-connections`));
  }

  async function handleAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    if (!account.trim()) {
      setError("アカウント/組織/グループを入力してください。");
      return;
    }
    setIsSubmitting(true);
    try {
      const body: GitConnectionRequestBody = {
        provider,
        authType,
        externalAccount: account.trim(),
        baseUrl: baseUrl.trim() || null,
        secretRef: secretRef.trim() || null,
      };
      await apiFetch(`/workspaces/${workspaceId}/git-connections`, { method: "POST", body: JSON.stringify(body) });
      await reload();
      setIsAdding(false);
      setAccount("");
      setBaseUrl("");
      setSecretRef("");
    } catch (err) {
      setError(formatApiErrorMessage(err, "Git 接続の追加に失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDeleteConfirmed() {
    if (!deleteTarget) return;
    setIsDeleting(true);
    setError(null);
    try {
      await apiFetch<void>(`/git-connections/${deleteTarget.id}`, { method: "DELETE" });
      await reload();
      setDeleteTarget(null);
    } catch (err) {
      setError(formatApiErrorMessage(err, "Git 接続の削除に失敗しました。"));
    } finally {
      setIsDeleting(false);
    }
  }

  if (!canManage) {
    return <p className="text-sm text-gray-500">Git 接続の管理はワークスペース管理者(ADMIN)のみ可能です。</p>;
  }

  if (!connections && !error) {
    return <p className="text-sm text-gray-500">読み込み中...</p>;
  }

  return (
    <div>
      {error && <ErrorMessage message={error} />}

      {connections && connections.length > 0 && (
        <ul className="mb-4 divide-y divide-gray-100 rounded-md border border-gray-200">
          {connections.map((c) => (
            <li key={c.id} className="flex items-center justify-between gap-2 px-3 py-2 text-sm">
              <div className="min-w-0">
                <span className="font-medium text-gray-900">{GIT_PROVIDER_LABELS[c.provider]}</span>
                <span className="ml-2 text-gray-600">{c.externalAccount ?? "-"}</span>
                <span className="ml-2 text-xs text-gray-500">
                  {GIT_AUTH_TYPE_LABELS[c.authType]}・{GIT_CONNECTION_STATUS_LABELS[c.status]}
                </span>
                {c.baseUrl && <span className="ml-2 truncate text-xs text-gray-400">{c.baseUrl}</span>}
              </div>
              <button
                type="button"
                aria-label={`${GIT_PROVIDER_LABELS[c.provider]} ${c.externalAccount ?? ""} を削除`}
                onClick={() => setDeleteTarget(c)}
                className="shrink-0 text-red-600 hover:underline"
              >
                削除
              </button>
            </li>
          ))}
        </ul>
      )}

      {connections && connections.length === 0 && (
        <p className="mb-4 text-sm text-gray-500">Git 接続がありません。</p>
      )}

      {!isAdding && (
        <Button type="button" variant="secondary" onClick={() => setIsAdding(true)}>
          Git 接続を追加
        </Button>
      )}

      {isAdding && (
        <form onSubmit={handleAdd} className="space-y-3 rounded-md border border-gray-200 p-4">
          <div className="flex flex-wrap gap-3">
            <div>
              <label htmlFor="gitProvider" className="mb-1 block text-sm font-medium text-gray-700">プロバイダ</label>
              <select
                id="gitProvider"
                value={provider}
                onChange={(e) => setProvider(e.target.value as GitProvider)}
                className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
              >
                <option value="GITHUB">GitHub</option>
                <option value="GITLAB">GitLab</option>
              </select>
            </div>
            <div>
              <label htmlFor="gitAuthType" className="mb-1 block text-sm font-medium text-gray-700">認証種別</label>
              <select
                id="gitAuthType"
                value={authType}
                onChange={(e) => setAuthType(e.target.value as GitAuthType)}
                className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
              >
                <option value="GITHUB_APP">GitHub App</option>
                <option value="OAUTH">OAuth</option>
                <option value="PAT">Personal Access Token</option>
                <option value="GROUP_TOKEN">Project/Group トークン</option>
              </select>
            </div>
          </div>
          <Input
            id="gitAccount"
            label="アカウント/組織/グループ"
            value={account}
            onChange={(e) => setAccount(e.target.value)}
            placeholder="acme"
          />
          <Input
            id="gitBaseUrl"
            label="ベースURL(self-managed のみ・任意)"
            value={baseUrl}
            onChange={(e) => setBaseUrl(e.target.value)}
            placeholder="https://gitlab.example.com"
          />
          <Input
            id="gitSecretRef"
            label="シークレット参照(Secrets Manager の名前/ARN・任意)"
            value={secretRef}
            onChange={(e) => setSecretRef(e.target.value)}
            placeholder="tms/git/acme"
          />
          <p className="text-xs text-gray-500">
            ※ トークンや秘密鍵そのものはここに入力せず、シークレットストアに保管した参照のみを指定します。
          </p>
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

      <Modal isOpen={deleteTarget !== null} title="Git 接続の削除" onClose={() => setDeleteTarget(null)}>
        <p>この Git 接続を削除しますか？連携中のリポジトリも解除されます。</p>
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
