"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Input } from "@/components/common/Input";
import { Modal } from "@/components/common/Modal";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { GIT_PROVIDER_LABELS, type GitConnection, type RepositoryLink, type RepositoryLinkRequestBody } from "@/types/git";

interface RepositoryLinksProps {
  projectId: string;
  workspaceId: number;
}

export function RepositoryLinks({ projectId, workspaceId }: RepositoryLinksProps) {
  const [links, setLinks] = useState<RepositoryLink[] | null>(null);
  const [connections, setConnections] = useState<GitConnection[]>([]);
  const [error, setError] = useState<string | null>(null);

  const [isAdding, setIsAdding] = useState(false);
  const [connectionId, setConnectionId] = useState("");
  const [repoFullName, setRepoFullName] = useState("");
  const [externalRepoId, setExternalRepoId] = useState("");
  const [defaultBranch, setDefaultBranch] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState<RepositoryLink | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  useEffect(() => {
    apiFetch<RepositoryLink[]>(`/projects/${projectId}/repository-links`)
      .then(setLinks)
      .catch(() => setError("連携リポジトリの取得に失敗しました。"));
    // 連携先の接続候補(同一ワークスペース)
    apiFetch<GitConnection[]>(`/workspaces/${workspaceId}/git-connections`)
      .then(setConnections)
      .catch(() => {
        // 接続が取得できなくても一覧表示は行う
      });
  }, [projectId, workspaceId]);

  async function reload() {
    setLinks(await apiFetch<RepositoryLink[]>(`/projects/${projectId}/repository-links`));
  }

  async function handleAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    if (!connectionId) {
      setError("接続を選択してください。");
      return;
    }
    if (!repoFullName.trim() || !externalRepoId.trim()) {
      setError("リポジトリ名とリポジトリIDを入力してください。");
      return;
    }
    setIsSubmitting(true);
    try {
      const body: RepositoryLinkRequestBody = {
        gitConnectionId: Number(connectionId),
        externalRepoId: externalRepoId.trim(),
        repoFullName: repoFullName.trim(),
        defaultBranch: defaultBranch.trim() || null,
      };
      await apiFetch(`/projects/${projectId}/repository-links`, { method: "POST", body: JSON.stringify(body) });
      await reload();
      setIsAdding(false);
      setConnectionId("");
      setRepoFullName("");
      setExternalRepoId("");
      setDefaultBranch("");
    } catch (err) {
      setError(formatApiErrorMessage(err, "リポジトリ連携の追加に失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDeleteConfirmed() {
    if (!deleteTarget) return;
    setIsDeleting(true);
    setError(null);
    try {
      await apiFetch<void>(`/repository-links/${deleteTarget.id}`, { method: "DELETE" });
      await reload();
      setDeleteTarget(null);
    } catch (err) {
      setError(formatApiErrorMessage(err, "リポジトリ連携の削除に失敗しました。"));
    } finally {
      setIsDeleting(false);
    }
  }

  function connectionLabel(id: number) {
    const c = connections.find((x) => x.id === id);
    return c ? `${GIT_PROVIDER_LABELS[c.provider]} / ${c.externalAccount ?? "-"}` : `接続#${id}`;
  }

  if (!links && !error) {
    return <p className="text-sm text-gray-500">読み込み中...</p>;
  }

  return (
    <div>
      {error && <ErrorMessage message={error} />}

      {links && links.length > 0 && (
        <ul className="mb-4 divide-y divide-gray-100 rounded-md border border-gray-200">
          {links.map((link) => (
            <li key={link.id} className="flex items-center justify-between gap-2 px-3 py-2 text-sm">
              <div className="min-w-0">
                <span className="font-medium text-gray-900">{link.repoFullName}</span>
                {link.defaultBranch && <span className="ml-2 text-xs text-gray-500">({link.defaultBranch})</span>}
                <span className="ml-2 text-xs text-gray-400">{connectionLabel(link.gitConnectionId)}</span>
              </div>
              <button
                type="button"
                aria-label={`${link.repoFullName} の連携を解除`}
                onClick={() => setDeleteTarget(link)}
                className="shrink-0 text-red-600 hover:underline"
              >
                解除
              </button>
            </li>
          ))}
        </ul>
      )}

      {links && links.length === 0 && <p className="mb-4 text-sm text-gray-500">連携リポジトリがありません。</p>}

      {!isAdding && (
        <Button type="button" variant="secondary" onClick={() => setIsAdding(true)}>
          リポジトリを連携
        </Button>
      )}

      {isAdding && (
        <form onSubmit={handleAdd} className="space-y-3 rounded-md border border-gray-200 p-4">
          <div>
            <label htmlFor="repoConnection" className="mb-1 block text-sm font-medium text-gray-700">接続</label>
            <select
              id="repoConnection"
              value={connectionId}
              onChange={(e) => setConnectionId(e.target.value)}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
            >
              <option value="">選択してください</option>
              {connections.map((c) => (
                <option key={c.id} value={c.id}>
                  {GIT_PROVIDER_LABELS[c.provider]} / {c.externalAccount ?? "-"}
                </option>
              ))}
            </select>
            {connections.length === 0 && (
              <p className="mt-1 text-xs text-gray-500">
                先にワークスペース設定で Git 接続を追加してください。
              </p>
            )}
          </div>
          <Input
            id="repoFullName"
            label="リポジトリ名(owner/repo)"
            value={repoFullName}
            onChange={(e) => setRepoFullName(e.target.value)}
            placeholder="acme/app"
          />
          <Input
            id="repoExternalId"
            label="リポジトリID(プロバイダ側の安定ID)"
            value={externalRepoId}
            onChange={(e) => setExternalRepoId(e.target.value)}
            placeholder="123456"
          />
          <Input
            id="repoDefaultBranch"
            label="既定ブランチ(任意)"
            value={defaultBranch}
            onChange={(e) => setDefaultBranch(e.target.value)}
            placeholder="main"
          />
          <div className="flex gap-3">
            <Button type="button" variant="secondary" onClick={() => setIsAdding(false)} disabled={isSubmitting}>
              キャンセル
            </Button>
            <Button type="submit" variant="primary" disabled={isSubmitting}>
              {isSubmitting ? "連携中..." : "連携"}
            </Button>
          </div>
        </form>
      )}

      <Modal isOpen={deleteTarget !== null} title="リポジトリ連携の解除" onClose={() => setDeleteTarget(null)}>
        <p>「{deleteTarget?.repoFullName}」の連携を解除しますか？</p>
        <div className="mt-6 flex justify-end gap-3">
          <Button type="button" variant="secondary" onClick={() => setDeleteTarget(null)} disabled={isDeleting}>
            キャンセル
          </Button>
          <Button type="button" variant="danger" onClick={handleDeleteConfirmed} disabled={isDeleting}>
            {isDeleting ? "解除中..." : "解除"}
          </Button>
        </div>
      </Modal>
    </div>
  );
}
