"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { clearSession, getAccessToken, getCurrentUser } from "@/lib/auth";
import type { CurrentUser } from "@/types/auth";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const [authState, setAuthState] = useState<
    { status: "checking" } | { status: "ready"; user: CurrentUser | null }
  >({ status: "checking" });

  useEffect(() => {
    if (!getAccessToken()) {
      router.replace("/login");
      return;
    }

    // localStorageはブラウザでしか参照できず、SSR時点では認証状態が分からないため、
    // マウント後にeffect内で確定させる(ハイドレーション不整合を避けるための意図的な設計)。
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setAuthState({ status: "ready", user: getCurrentUser() });
  }, [router]);

  function handleLogout() {
    clearSession();
    router.replace("/login");
  }

  if (authState.status === "checking") {
    return null;
  }

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex items-center justify-between border-b border-gray-200 bg-white px-6 py-3">
        <span className="font-bold text-gray-900">案件・タスク管理システム</span>
        <span className="text-sm text-gray-600">{authState.user?.name} ▼</span>
      </header>

      <div className="flex flex-1">
        <nav className="w-48 shrink-0 border-r border-gray-200 bg-white p-4">
          <Link
            href="/projects"
            className="block rounded-md px-3 py-2 text-sm text-gray-700 hover:bg-gray-100"
          >
            プロジェクト
          </Link>
          <button
            type="button"
            onClick={handleLogout}
            className="block w-full rounded-md px-3 py-2 text-left text-sm text-gray-700 hover:bg-gray-100"
          >
            ログアウト
          </button>
        </nav>

        <main className="flex-1 bg-gray-50 p-6">{children}</main>
      </div>
    </div>
  );
}
