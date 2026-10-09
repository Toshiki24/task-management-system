"use client";

import { useEffect, useState } from "react";
import { apiFetch } from "@/lib/api";
import {
  GIT_LINK_STATE_LABELS,
  GIT_LINK_TYPE_LABELS,
  type TaskGitLink,
} from "@/types/git";

interface TaskGitLinksProps {
  taskId: string;
}

/** タスクに紐づく Git リンク(ブランチ/PR/MR/コミット)を表示する(M4 §7)。 */
export function TaskGitLinks({ taskId }: TaskGitLinksProps) {
  const [links, setLinks] = useState<TaskGitLink[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<TaskGitLink[]>(`/tasks/${taskId}/git/links`)
      .then(setLinks)
      .catch(() => setError("Git リンクの取得に失敗しました。"));
  }, [taskId]);

  return (
    <div>
      <h2 className="mb-2 text-base font-bold text-gray-900">Git 連携</h2>

      {error && <p className="mb-2 text-sm text-red-600">{error}</p>}

      {links && links.length === 0 && (
        <p className="text-sm text-gray-500">紐づく Git の変更はまだありません。</p>
      )}

      {links && links.length > 0 && (
        <ul className="space-y-2">
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
    </div>
  );
}
