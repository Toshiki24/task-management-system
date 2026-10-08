"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { fetchCurrentUser } from "@/lib/auth";
import { formatDateTime } from "@/lib/format";
import { renderMarkdownToHtml } from "@/lib/markdown";
import type { Comment, CommentRequestBody } from "@/types/comment";

interface CommentListProps {
  taskId: string | number;
}

export function CommentList({ taskId }: CommentListProps) {
  const [comments, setComments] = useState<Comment[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [newComment, setNewComment] = useState("");
  const [postError, setPostError] = useState<string | null>(null);
  const [isPosting, setIsPosting] = useState(false);
  const [currentUserId, setCurrentUserId] = useState<number | null>(null);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editingText, setEditingText] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<Comment[]>(`/tasks/${taskId}/comments`)
      .then(setComments)
      .catch(() => setLoadError("コメントの取得に失敗しました。"));
    fetchCurrentUser()
      .then((user) => setCurrentUserId(user?.id ?? null))
      .catch(() => setCurrentUserId(null));
  }, [taskId]);

  async function reload() {
    setComments(await apiFetch<Comment[]>(`/tasks/${taskId}/comments`));
  }

  async function handlePost(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPostError(null);

    if (!newComment.trim()) {
      setPostError("コメントを入力してください。");
      return;
    }

    setIsPosting(true);
    try {
      const body: CommentRequestBody = { comment: newComment };
      await apiFetch(`/tasks/${taskId}/comments`, { method: "POST", body: JSON.stringify(body) });
      await reload();
      setNewComment("");
    } catch (err) {
      setPostError(formatApiErrorMessage(err, "コメントの投稿に失敗しました。"));
    } finally {
      setIsPosting(false);
    }
  }

  function startEdit(comment: Comment) {
    setActionError(null);
    setEditingId(comment.id);
    setEditingText(comment.comment ?? "");
  }

  async function handleEditSave(commentId: number) {
    if (!editingText.trim()) return;
    setActionError(null);
    try {
      await apiFetch(`/tasks/${taskId}/comments/${commentId}`, {
        method: "PATCH",
        body: JSON.stringify({ comment: editingText } satisfies CommentRequestBody),
      });
      setEditingId(null);
      setEditingText("");
      await reload();
    } catch (err) {
      setActionError(formatApiErrorMessage(err, "コメントの更新に失敗しました。"));
    }
  }

  async function handleDelete(commentId: number) {
    setActionError(null);
    try {
      await apiFetch(`/tasks/${taskId}/comments/${commentId}`, { method: "DELETE" });
      await reload();
    } catch (err) {
      setActionError(formatApiErrorMessage(err, "コメントの削除に失敗しました。"));
    }
  }

  return (
    <div>
      {loadError && <ErrorMessage message={loadError} />}
      {!comments && !loadError && <Loading />}
      {actionError && <ErrorMessage message={actionError} />}

      {comments && comments.length > 0 && (
        <ul className="mb-4 space-y-4">
          {comments.map((comment) => {
            const canEdit = currentUserId !== null && comment.userId === currentUserId && !comment.isDeleted;
            return (
              <li key={comment.id} className="border-b border-gray-100 pb-3">
                <div className="flex items-center justify-between">
                  <p className="text-sm font-semibold text-gray-900">
                    {comment.userName}
                    {comment.edited && !comment.isDeleted && (
                      <span className="ml-2 text-xs font-normal text-gray-400">(編集済み)</span>
                    )}
                  </p>
                  {canEdit && editingId !== comment.id && (
                    <div className="flex gap-2 text-xs">
                      <button type="button" onClick={() => startEdit(comment)} className="text-blue-600 hover:underline">
                        編集
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDelete(comment.id)}
                        className="text-red-600 hover:underline"
                        aria-label="コメントを削除"
                      >
                        削除
                      </button>
                    </div>
                  )}
                </div>

                {editingId === comment.id ? (
                  <div className="mt-1 space-y-2">
                    <textarea
                      rows={2}
                      maxLength={1000}
                      value={editingText}
                      onChange={(e) => setEditingText(e.target.value)}
                      aria-label="コメントを編集"
                      className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                    />
                    <div className="flex justify-end gap-2">
                      <Button type="button" variant="secondary" onClick={() => setEditingId(null)}>
                        キャンセル
                      </Button>
                      <Button type="button" variant="primary" onClick={() => handleEditSave(comment.id)}>
                        更新
                      </Button>
                    </div>
                  </div>
                ) : comment.isDeleted ? (
                  <p className="mt-1 text-sm italic text-gray-400">削除されたコメント</p>
                ) : (
                  <div
                    className="mt-1 break-words text-sm text-gray-700 [&_a]:break-all"
                    // 本文はサニタイズ済みの限定 Markdown(lib/markdown で全エスケープ後に限定タグのみ生成)。生の HTML は挿入されない
                    // eslint-disable-next-line react/no-danger
                    dangerouslySetInnerHTML={{ __html: renderMarkdownToHtml(comment.comment ?? "") }}
                  />
                )}

                <p className="mt-1 text-xs text-gray-400">{formatDateTime(comment.createdAt)}</p>
              </li>
            );
          })}
        </ul>
      )}

      {comments && comments.length === 0 && (
        <p className="mb-4 text-sm text-gray-500">コメントはまだありません。</p>
      )}

      <form onSubmit={handlePost} className="space-y-2">
        <textarea
          rows={2}
          maxLength={1000}
          value={newComment}
          onChange={(event) => setNewComment(event.target.value)}
          placeholder="コメントを入力してください"
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
        <p className="text-xs text-gray-400">Markdown(**太字**、*斜体*、`コード`、[リンク](https://...))が使えます。</p>
        {postError && <ErrorMessage message={postError} />}
        <div className="flex justify-end">
          <Button type="submit" variant="primary" disabled={isPosting}>
            {isPosting ? "投稿中..." : "投稿"}
          </Button>
        </div>
      </form>
    </div>
  );
}
