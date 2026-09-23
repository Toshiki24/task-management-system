"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { formatDateTime } from "@/lib/format";
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

  useEffect(() => {
    apiFetch<Comment[]>(`/tasks/${taskId}/comments`)
      .then(setComments)
      .catch(() => setLoadError("コメントの取得に失敗しました。"));
  }, [taskId]);

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
      await apiFetch(`/tasks/${taskId}/comments`, {
        method: "POST",
        body: JSON.stringify(body),
      });

      setComments(await apiFetch<Comment[]>(`/tasks/${taskId}/comments`));
      setNewComment("");
    } catch (err) {
      setPostError(formatApiErrorMessage(err, "コメントの投稿に失敗しました。"));
    } finally {
      setIsPosting(false);
    }
  }

  return (
    <div>
      {loadError && <ErrorMessage message={loadError} />}
      {!comments && !loadError && <Loading />}

      {comments && comments.length > 0 && (
        <ul className="mb-4 space-y-4">
          {comments.map((comment) => (
            <li key={comment.id} className="border-b border-gray-100 pb-3">
              <p className="text-sm font-semibold text-gray-900">
                {comment.userName}
              </p>
              <p className="mt-1 whitespace-pre-wrap text-sm text-gray-700">
                {comment.comment}
              </p>
              <p className="mt-1 text-xs text-gray-400">
                {formatDateTime(comment.createdAt)}
              </p>
            </li>
          ))}
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
