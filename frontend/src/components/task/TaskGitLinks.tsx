"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/common/Button";
import { Input } from "@/components/common/Input";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import {
  GIT_LINK_STATE_LABELS,
  GIT_LINK_TYPE_LABELS,
  type RepositoryLink,
  type TaskGitLink,
} from "@/types/git";

interface TaskGitLinksProps {
  taskId: string;
  projectId: number;
}

/** タスクに紐づく Git リンク表示と、ブランチ/PR の作成(M4 §7)。 */
export function TaskGitLinks({ taskId, projectId }: TaskGitLinksProps) {
  const [links, setLinks] = useState<TaskGitLink[] | null>(null);
  const [repos, setRepos] = useState<RepositoryLink[]>([]);
  const [repoId, setRepoId] = useState("");
  const [sourceBranch, setSourceBranch] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    apiFetch<TaskGitLink[]>(`/tasks/${taskId}/git/links`)
      .then(setLinks)
      .catch(() => setError("Git リンクの取得に失敗しました。"));
    apiFetch<RepositoryLink[]>(`/projects/${projectId}/repository-links`)
      .then((list) => {
        setRepos(list);
        if (list.length > 0) setRepoId(String(list[0].id));
      })
      .catch(() => {
        // 連携が取れなくても表示は行う
      });
  }, [taskId, projectId]);

  async function reload() {
    setLinks(await apiFetch<TaskGitLink[]>(`/tasks/${taskId}/git/links`));
  }

  async function createBranch() {
    if (!repoId) return;
    setError(null);
    setBusy(true);
    try {
      await apiFetch(`/tasks/${taskId}/git/branch`, {
        method: "POST",
        body: JSON.stringify({ repositoryLinkId: Number(repoId) }),
      });
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "ブランチの作成に失敗しました。"));
    } finally {
      setBusy(false);
    }
  }

  async function createPullRequest() {
    if (!repoId) return;
    if (!sourceBranch.trim()) {
      setError("PR 作成にはソースブランチを入力してください。");
      return;
    }
    setError(null);
    setBusy(true);
    try {
      await apiFetch(`/tasks/${taskId}/git/pull-request`, {
        method: "POST",
        body: JSON.stringify({ repositoryLinkId: Number(repoId), sourceBranch: sourceBranch.trim() }),
      });
      setSourceBranch("");
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "PR の作成に失敗しました。"));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h2 className="mb-2 text-base font-bold text-gray-900">Git 連携</h2>

      {error && <p className="mb-2 text-sm text-red-600">{error}</p>}

      {links && links.length === 0 && (
        <p className="mb-3 text-sm text-gray-500">紐づく Git の変更はまだありません。</p>
      )}

      {links && links.length > 0 && (
        <ul className="mb-3 space-y-2">
          {links.map((link) => {
            const label = (
              <>
                <span className="rounded bg-gray-100 px-1.5 py-0.5 text-[10px] font-medium text-gray-600">
                  {GIT_LINK_TYPE_LABELS[link.linkType]}
                </span>
                <span className="ml-2 text-gray-900">{link.title ?? link.externalRef}</span>
                {link.state && (
                  <span className="ml-2 text-xs text-gray-500">{GIT_LINK_STATE_LABELS[link.state]}</span>
                )}
              </>
            );
            return (
              <li key={link.id} className="text-sm">
                {link.url ? (
                  <a href={link.url} target="_blank" rel="noopener noreferrer" className="hover:underline">
                    {label}
                  </a>
                ) : (
                  <span>{label}</span>
                )}
              </li>
            );
          })}
        </ul>
      )}

      {repos.length > 0 ? (
        <div className="space-y-2 rounded-md border border-gray-200 p-3">
          <div>
            <label htmlFor="gitActionRepo" className="mb-1 block text-xs font-medium text-gray-700">対象リポジトリ</label>
            <select
              id="gitActionRepo"
              value={repoId}
              onChange={(e) => setRepoId(e.target.value)}
              className="w-full rounded-md border border-gray-300 px-2 py-1 text-sm text-gray-900"
            >
              {repos.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.repoFullName}
                </option>
              ))}
            </select>
          </div>
          <div className="flex flex-wrap items-end gap-2">
            <Button type="button" variant="secondary" onClick={createBranch} disabled={busy}>
              ブランチ作成
            </Button>
            <div className="w-48">
              <Input
                id="gitPrSource"
                label="PR のソースブランチ"
                value={sourceBranch}
                onChange={(e) => setSourceBranch(e.target.value)}
                placeholder="feature/x"
              />
            </div>
            <Button type="button" variant="secondary" onClick={createPullRequest} disabled={busy}>
              PR 作成
            </Button>
          </div>
        </div>
      ) : (
        <p className="text-xs text-gray-500">
          プロジェクトにリポジトリを連携すると、ブランチや PR を作成できます。
        </p>
      )}
    </div>
  );
}
