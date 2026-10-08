"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { apiFetch } from "@/lib/api";
import { formatDateTime } from "@/lib/format";
import { useLiveRefresh } from "@/lib/useLiveRefresh";
import type { AppNotification, NotificationType } from "@/types/notification";

interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

const MESSAGE: Record<NotificationType, string> = {
  MENTION: "があなたをメンションしました",
  ASSIGNED: "があなたを担当に設定しました",
  STATUS_CHANGED: "が状態を変更しました",
  COMMENT: "がコメントしました",
  DUE_SOON: "の期限が近づいています",
  DUE_OVERDUE: "の期限が過ぎています",
};

export function NotificationBell() {
  const router = useRouter();
  const [unread, setUnread] = useState(0);
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<AppNotification[] | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const loadUnread = useCallback(() => {
    apiFetch<{ count: number }>("/me/notifications/unread-count")
      .then((r) => setUnread(r.count))
      .catch(() => {
        // 取得失敗は無視(バッジを出さないだけ)
      });
  }, []);

  // マウント時に未読数を取得する
  useEffect(() => {
    loadUnread();
  }, [loadUnread]);

  // フォーカス時＋一定間隔で未読数を最新化する(M3 §7 リアルタイム更新)
  useLiveRefresh(loadUnread, { intervalMs: 30000 });

  // 外側クリックで閉じる
  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (open && containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", onClick);
    return () => document.removeEventListener("mousedown", onClick);
  }, [open]);

  async function toggleOpen() {
    const next = !open;
    setOpen(next);
    if (next) {
      try {
        const page = await apiFetch<Paged<AppNotification>>("/me/notifications?pageSize=10");
        setItems(page.items);
      } catch {
        setItems([]);
      }
    }
  }

  async function handleItemClick(item: AppNotification) {
    try {
      if (!item.isRead) {
        await apiFetch(`/me/notifications/${item.id}/read`, { method: "POST" });
      }
    } catch {
      // 既読化に失敗しても遷移は行う
    }
    setOpen(false);
    loadUnread();
    if (item.taskId) {
      router.push(`/tasks/${item.taskId}`);
    }
  }

  async function markAllRead() {
    try {
      await apiFetch("/me/notifications/read-all", { method: "POST" });
      setItems((prev) => (prev ? prev.map((n) => ({ ...n, isRead: true })) : prev));
      setUnread(0);
    } catch {
      // 失敗時は何もしない
    }
  }

  return (
    <div className="relative" ref={containerRef}>
      <button
        type="button"
        onClick={toggleOpen}
        aria-label="通知"
        className="relative rounded-md p-1.5 text-gray-500 hover:bg-gray-50"
      >
        <span aria-hidden className="text-lg">🔔</span>
        {unread > 0 && (
          <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-500 px-1 text-[10px] font-bold text-white">
            {unread > 99 ? "99+" : unread}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 z-50 mt-2 w-80 overflow-hidden rounded-lg border border-gray-200 bg-white shadow-xl">
          <div className="flex items-center justify-between border-b border-gray-100 px-4 py-2">
            <span className="text-sm font-semibold text-gray-900">通知</span>
            <button type="button" onClick={markAllRead} className="text-xs text-blue-600 hover:underline">
              すべて既読
            </button>
          </div>
          <ul className="max-h-96 overflow-y-auto">
            {items && items.length === 0 && (
              <li className="px-4 py-6 text-center text-sm text-gray-500">通知はありません。</li>
            )}
            {items?.map((item) => (
              <li key={item.id}>
                <button
                  type="button"
                  onClick={() => handleItemClick(item)}
                  className={`flex w-full gap-2 px-4 py-2.5 text-left text-sm hover:bg-gray-50 ${
                    item.isRead ? "" : "bg-blue-50/50"
                  }`}
                >
                  <span
                    aria-hidden
                    className={`mt-1.5 h-2 w-2 shrink-0 rounded-full ${item.isRead ? "bg-transparent" : "bg-blue-500"}`}
                  />
                  <span className="min-w-0">
                    <span className="block text-gray-900">
                      <span className="font-medium">{item.actorName ?? "誰か"}</span>
                      {MESSAGE[item.type]}
                    </span>
                    {item.taskTitle && (
                      <span className="block truncate text-xs text-gray-500">「{item.taskTitle}」</span>
                    )}
                    <span className="block text-xs text-gray-400">{formatDateTime(item.createdAt)}</span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
