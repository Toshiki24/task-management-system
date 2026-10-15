"use client";

import { useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { login, verifyMfa } from "@/lib/auth";
import { getSetupStatus } from "@/lib/setup";

interface SsoConnection {
  id: number;
  name: string;
}

const SSO_ERROR_MESSAGES: Record<string, string> = {
  denied: "SSO ログインがキャンセルされました。",
  auth: "SSO 認証に失敗しました。アカウントが許可されているかご確認ください。",
  start: "SSO の開始に失敗しました。接続設定をご確認ください。",
  unavailable: "SSO プロバイダに接続できませんでした。時間をおいて再度お試しください。",
};

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  // MFA 有効ユーザーは、パスワード認証後にコード入力ステップへ進む
  const [mfaToken, setMfaToken] = useState<string | null>(null);
  const [mfaCode, setMfaCode] = useState("");
  const [ssoConnections, setSsoConnections] = useState<SsoConnection[]>([]);
  const [ssoError, setSsoError] = useState<string | null>(null);

  // 有効な SSO 接続があればログインボタンを出す
  useEffect(() => {
    apiFetch<SsoConnection[]>("/auth/sso/connections")
      .then(setSsoConnections)
      .catch(() => setSsoConnections([]));
  }, []);

  // SSO コールバックからのエラー表示(/login?sso_error=...)
  useEffect(() => {
    const code = new URLSearchParams(window.location.search).get("sso_error");
    if (code) {
      setSsoError(SSO_ERROR_MESSAGES[code] ?? "SSO ログインに失敗しました。");
    }
  }, []);

  // 初回セットアップ(管理者未作成)の場合はセットアップ画面へ誘導する(M5 §8)
  useEffect(() => {
    let cancelled = false;
    getSetupStatus().then((status) => {
      if (!cancelled && status === "required") {
        router.replace("/setup");
      }
    });
    return () => {
      cancelled = true;
    };
  }, [router]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      const result = await login({ email, password });
      if (result.mfaRequired) {
        setMfaToken(result.mfaToken);
      } else {
        router.push("/projects");
      }
    } catch (err) {
      setError(
        formatApiErrorMessage(
          err,
          "ログインに失敗しました。時間をおいて再度お試しください。",
        ),
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleMfaSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!mfaToken) return;
    setError(null);
    setIsSubmitting(true);

    try {
      await verifyMfa(mfaToken, mfaCode.trim());
      router.push("/projects");
    } catch (err) {
      setError(formatApiErrorMessage(err, "コードの検証に失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-gray-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-gray-200 bg-white p-8 shadow-sm">
        <h1 className="mb-8 text-center text-xl font-bold text-gray-900">
          案件・タスク管理システム
        </h1>

        {mfaToken ? (
          <form onSubmit={handleMfaSubmit} className="space-y-5">
            <p className="text-sm text-gray-600">
              認証アプリに表示されている6桁のコード、またはリカバリコードを入力してください。
            </p>
            <div>
              <label htmlFor="mfaCode" className="mb-1 block text-sm font-medium text-gray-700">
                認証コード
              </label>
              <input
                id="mfaCode"
                name="mfaCode"
                type="text"
                inputMode="numeric"
                autoComplete="one-time-code"
                autoFocus
                required
                value={mfaCode}
                onChange={(event) => setMfaCode(event.target.value)}
                placeholder="123456"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
              />
            </div>
            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isSubmitting ? "確認中..." : "認証して続行"}
            </button>
            {error && (
              <p role="alert" className="text-sm text-red-600">
                {error}
              </p>
            )}
          </form>
        ) : (
        <form onSubmit={handleSubmit} className="space-y-5">
          <div>
            <label
              htmlFor="email"
              className="mb-1 block text-sm font-medium text-gray-700"
            >
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
              onChange={(event) => setEmail(event.target.value)}
              placeholder="user@example.com"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          <div>
            <label
              htmlFor="password"
              className="mb-1 block text-sm font-medium text-gray-700"
            >
              パスワード
            </label>
            <input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="********"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmitting ? "ログイン中..." : "ログイン"}
          </button>

          {error && (
            <p role="alert" className="text-sm text-red-600">
              {error}
            </p>
          )}
        </form>
        )}

        {!mfaToken && (ssoConnections.length > 0 || ssoError) && (
          <div className="mt-6 space-y-3">
            {ssoError && (
              <p role="alert" className="text-sm text-red-600">
                {ssoError}
              </p>
            )}
            {ssoConnections.length > 0 && (
              <>
                <div className="flex items-center gap-3 text-xs text-gray-400">
                  <span className="h-px flex-1 bg-gray-200" />
                  または
                  <span className="h-px flex-1 bg-gray-200" />
                </div>
                {ssoConnections.map((connection) => (
                  <a
                    key={connection.id}
                    href={`/api/bff/auth/sso/${connection.id}/start`}
                    className="block w-full rounded-md border border-gray-300 bg-white px-4 py-2 text-center text-sm font-semibold text-gray-700 transition hover:bg-gray-50"
                  >
                    {connection.name} でログイン
                  </a>
                ))}
              </>
            )}
          </div>
        )}
      </div>
    </main>
  );
}
