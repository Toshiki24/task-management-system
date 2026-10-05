"use client";

import { Suspense, useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Input } from "@/components/common/Input";
import { Loading } from "@/components/common/Loading";
import { apiFetch, ApiError, formatApiErrorMessage } from "@/lib/api";
import type { AcceptInvitationBody, AcceptInvitationResult, InvitationPreview } from "@/types/invitation";

function AcceptInvitation() {
  const router = useRouter();
  const token = useSearchParams().get("token");

  const [preview, setPreview] = useState<InvitationPreview | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [done, setDone] = useState(false);

  useEffect(() => {
    if (!token) {
      setLoadError("招待トークンがありません。");
      return;
    }
    apiFetch<InvitationPreview>(`/invitations/${encodeURIComponent(token)}`)
      .then(setPreview)
      .catch((err) => {
        setLoadError(
          err instanceof ApiError && err.status === 404
            ? "招待が無効か、有効期限が切れています。"
            : "招待情報の取得に失敗しました。",
        );
      });
  }, [token]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token || !preview) {
      return;
    }
    setError(null);

    // 新規ユーザーは名前とパスワードが必須
    if (!preview.isExistingUser && (!name.trim() || password.length < 8)) {
      setError("名前と8文字以上のパスワードを入力してください。");
      return;
    }

    setIsSubmitting(true);
    try {
      const body: AcceptInvitationBody = preview.isExistingUser
        ? {}
        : { name: name.trim(), password };
      await apiFetch<AcceptInvitationResult>(`/invitations/${encodeURIComponent(token)}/accept`, {
        method: "POST",
        body: JSON.stringify(body),
      });
      setDone(true);
    } catch (err) {
      setError(formatApiErrorMessage(err, "参加に失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  if (loadError) {
    return (
      <div className="mx-auto mt-16 max-w-md px-4">
        <ErrorMessage message={loadError} />
        <Link href="/login" className="mt-4 inline-block text-sm text-blue-600 hover:underline">
          ログイン画面へ
        </Link>
      </div>
    );
  }

  if (!preview) {
    return <Loading />;
  }

  if (done) {
    return (
      <div className="mx-auto mt-16 max-w-md px-4 text-center">
        <h1 className="mb-2 text-lg font-bold text-gray-900">ワークスペースに参加しました</h1>
        <p className="mb-6 text-sm text-gray-600">
          「{preview.workspaceName}」に参加しました。ログインして利用を開始してください。
        </p>
        <Button type="button" variant="primary" onClick={() => router.push("/login")}>
          ログイン画面へ
        </Button>
      </div>
    );
  }

  return (
    <div className="mx-auto mt-16 max-w-md px-4">
      <h1 className="mb-2 text-lg font-bold text-gray-900">ワークスペースへの招待</h1>
      <p className="mb-6 text-sm text-gray-600">
        <span className="font-semibold">{preview.workspaceName}</span> に{" "}
        <span className="font-semibold">{preview.role}</span> として招待されています（{preview.email}）。
      </p>

      <form onSubmit={handleSubmit} className="space-y-5 rounded-lg bg-white p-6 shadow-sm">
        {preview.isExistingUser ? (
          <p className="text-sm text-gray-700">
            既存のアカウントで参加します。「参加する」を押してください。
          </p>
        ) : (
          <>
            <p className="text-sm text-gray-700">アカウントを作成して参加します。</p>
            <Input
              id="name"
              label="名前 *"
              required
              maxLength={100}
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
            <Input
              id="password"
              type="password"
              label="パスワード *（8文字以上）"
              required
              minLength={8}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </>
        )}

        {error && <ErrorMessage message={error} />}

        <Button type="submit" variant="primary" disabled={isSubmitting}>
          {isSubmitting ? "処理中..." : "参加する"}
        </Button>
      </form>
    </div>
  );
}

export default function AcceptInvitationPage() {
  return (
    <Suspense fallback={<Loading />}>
      <AcceptInvitation />
    </Suspense>
  );
}
