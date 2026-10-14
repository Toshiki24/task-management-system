"use client";

import { useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { createInitialAdmin, getSetupStatus } from "@/lib/setup";

export default function SetupPage() {
  const router = useRouter();
  const [phase, setPhase] = useState<"checking" | "ready" | "unknown" | "done">("checking");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // 直接アクセスされても、セットアップ不要なら通常ログインへ誘導する(サーバー判定に従う)
  useEffect(() => {
    let cancelled = false;
    getSetupStatus().then((status) => {
      if (cancelled) return;
      if (status === "required") setPhase("ready");
      else if (status === "unknown") setPhase("unknown");
      else router.replace("/login");
    });
    return () => {
      cancelled = true;
    };
  }, [router]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (password.length < 12) {
      setError("パスワードは12文字以上で入力してください。");
      return;
    }
    if (password !== confirm) {
      setError("パスワードと確認用パスワードが一致しません。");
      return;
    }

    setIsSubmitting(true);
    try {
      await createInitialAdmin({ name, email, password });
      setPhase("done");
      // 自動ログインはせず、通常のログイン画面へ誘導する
      setTimeout(() => router.replace("/login"), 1500);
    } catch (err) {
      setError(err instanceof Error ? err.message : "管理者の作成に失敗しました。");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-gray-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-8 shadow-sm">
        <h1 className="mb-2 text-center text-xl font-bold text-gray-900">案件・タスク管理システム</h1>
        <p className="mb-8 text-center text-sm text-gray-500">初期セットアップ（管理者アカウントの作成）</p>

        {phase === "checking" && <p className="text-center text-sm text-gray-500">確認中...</p>}

        {phase === "unknown" && (
          <p role="alert" className="text-sm text-red-600">
            セットアップ状態を確認できませんでした。データベースの起動状況を確認し、時間をおいて再度お試しください。
          </p>
        )}

        {phase === "done" && (
          <p className="text-sm text-gray-700">
            管理者アカウントを作成しました。ログイン画面に移動します…
          </p>
        )}

        {phase === "ready" && (
          <form onSubmit={handleSubmit} className="space-y-5">
            <div>
              <label htmlFor="name" className="mb-1 block text-sm font-medium text-gray-700">
                管理者名
              </label>
              <input
                id="name"
                name="name"
                type="text"
                required
                maxLength={100}
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="管理者"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>

            <div>
              <label htmlFor="email" className="mb-1 block text-sm font-medium text-gray-700">
                メールアドレス
              </label>
              <input
                id="email"
                name="email"
                type="email"
                autoComplete="username"
                required
                maxLength={255}
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="admin@example.com"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>

            <div>
              <label htmlFor="password" className="mb-1 block text-sm font-medium text-gray-700">
                パスワード（12文字以上）
              </label>
              <input
                id="password"
                name="password"
                type="password"
                autoComplete="new-password"
                required
                minLength={12}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="********"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>

            <div>
              <label htmlFor="confirm" className="mb-1 block text-sm font-medium text-gray-700">
                パスワード（確認）
              </label>
              <input
                id="confirm"
                name="confirm"
                type="password"
                autoComplete="new-password"
                required
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                placeholder="********"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isSubmitting ? "作成中..." : "管理者を作成する"}
            </button>

            {error && (
              <p role="alert" className="text-sm text-red-600">
                {error}
              </p>
            )}
          </form>
        )}
      </div>
    </main>
  );
}
