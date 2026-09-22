"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Header } from "@/components/layout/Header";
import { Sidebar } from "@/components/layout/Sidebar";
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
      <Header userName={authState.user?.name} />
      <div className="flex flex-1">
        <Sidebar onLogout={handleLogout} />
        <main className="flex-1 bg-gray-50 p-6">{children}</main>
      </div>
    </div>
  );
}
