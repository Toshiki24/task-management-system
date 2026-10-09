"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { CommandPalette } from "@/components/layout/CommandPalette";
import { Header } from "@/components/layout/Header";
import { Sidebar } from "@/components/layout/Sidebar";
import { fetchCurrentUser, logout } from "@/lib/auth";
import type { CurrentUser } from "@/types/auth";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const [authState, setAuthState] = useState<
    { status: "checking" } | { status: "ready"; user: CurrentUser | null }
  >({ status: "checking" });
  const [paletteOpen, setPaletteOpen] = useState(false);

  useEffect(() => {
    // ログイン状態はHttpOnlyのCookieで管理しており、JavaScriptからは分からないため、BFFに問い合わせて確定させる
    let cancelled = false;
    fetchCurrentUser()
      .then((user) => {
        if (cancelled) return;
        if (user) {
          setAuthState({ status: "ready", user });
        } else {
          router.replace("/login");
        }
      })
      .catch(() => {
        if (!cancelled) router.replace("/login");
      });
    return () => {
      cancelled = true;
    };
  }, [router]);

  async function handleLogout() {
    try {
      await logout();
    } finally {
      router.replace("/login");
    }
  }

  if (authState.status === "checking") {
    return null;
  }

  return (
    <div className="flex min-h-screen flex-col">
      <Header userName={authState.user?.name} onOpenCommand={() => setPaletteOpen(true)} />
      <div className="flex flex-1">
        <Sidebar onLogout={handleLogout} isSystemAdmin={authState.user?.isSystemAdmin ?? false} />
        <main className="flex-1 bg-gray-50 p-6">{children}</main>
      </div>
      <CommandPalette open={paletteOpen} onOpenChange={setPaletteOpen} />
    </div>
  );
}
