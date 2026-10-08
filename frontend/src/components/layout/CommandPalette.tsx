"use client";

import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { useRouter } from "next/navigation";
import { apiFetch } from "@/lib/api";
import type { TaskSearchResult } from "@/types/task";

interface CommandPaletteProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

interface NavCommand {
  id: string;
  label: string;
  href: string;
}

const NAV_COMMANDS: NavCommand[] = [
  { id: "nav-mytasks", label: "マイタスク", href: "/me/tasks" },
  { id: "nav-projects", label: "プロジェクト一覧", href: "/projects" },
];

export function CommandPalette({ open, onOpenChange }: CommandPaletteProps) {
  const router = useRouter();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<TaskSearchResult[]>([]);
  const [activeIndex, setActiveIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  // Cmd/Ctrl+K で開閉、Esc で閉じる(全画面共通)
  useEffect(() => {
    function onKeyDown(e: globalThis.KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        onOpenChange(!open);
      } else if (e.key === "Escape" && open) {
        onOpenChange(false);
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [open, onOpenChange]);

  // 開いたら入力をリセットしてフォーカスする
  useEffect(() => {
    if (open) {
      setQuery("");
      setActiveIndex(0);
      // レンダリング後にフォーカスする
      requestAnimationFrame(() => inputRef.current?.focus());
    }
  }, [open]);

  // クエリ変化でタスクを横断検索する(デバウンス)
  useEffect(() => {
    if (!open) return;
    const handle = setTimeout(() => {
      apiFetch<TaskSearchResult[]>(`/me/search/tasks?keyword=${encodeURIComponent(query)}&limit=10`)
        .then((tasks) => setResults(tasks))
        .catch(() => setResults([]));
    }, 200);
    return () => clearTimeout(handle);
  }, [open, query]);

  const navMatches = useMemo(() => {
    const q = query.trim().toLowerCase();
    return q === "" ? NAV_COMMANDS : NAV_COMMANDS.filter((c) => c.label.toLowerCase().includes(q));
  }, [query]);

  // ナビ + タスクの統合リスト(キーボード操作の添字はこの順)
  const items = useMemo(
    () => [
      ...navMatches.map((c) => ({ kind: "nav" as const, key: c.id, label: c.label, href: c.href })),
      ...results.map((t) => ({
        kind: "task" as const,
        key: `task-${t.id}`,
        label: t.title,
        href: `/tasks/${t.id}`,
        sub: t.projectName,
      })),
    ],
    [navMatches, results],
  );

  useEffect(() => {
    // 候補が変わったら選択位置を先頭へ戻す
    setActiveIndex(0);
  }, [query, results]);

  if (!open) return null;

  function go(href: string) {
    onOpenChange(false);
    router.push(href);
  }

  function onInputKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActiveIndex((i) => Math.min(i + 1, Math.max(items.length - 1, 0)));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActiveIndex((i) => Math.max(i - 1, 0));
    } else if (e.key === "Enter") {
      e.preventDefault();
      const item = items[activeIndex];
      if (item) go(item.href);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center bg-black/40 pt-24"
      onClick={() => onOpenChange(false)}
    >
      <div
        role="dialog"
        aria-label="コマンドパレット"
        className="w-full max-w-lg overflow-hidden rounded-lg bg-white shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <input
          ref={inputRef}
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={onInputKeyDown}
          placeholder="コマンド・タスクを検索..."
          aria-label="コマンド・タスクを検索"
          className="w-full border-b border-gray-200 px-4 py-3 text-sm text-gray-900 focus:outline-none"
        />
        <ul className="max-h-80 overflow-y-auto py-1">
          {items.length === 0 && (
            <li className="px-4 py-3 text-sm text-gray-500">一致する項目がありません。</li>
          )}
          {items.map((item, index) => (
            <li key={item.key}>
              <button
                type="button"
                onMouseEnter={() => setActiveIndex(index)}
                onClick={() => go(item.href)}
                className={`flex w-full items-center justify-between gap-2 px-4 py-2 text-left text-sm ${
                  index === activeIndex ? "bg-blue-50" : "hover:bg-gray-50"
                }`}
              >
                <span className="truncate text-gray-900">
                  {item.kind === "nav" ? `→ ${item.label}` : item.label}
                </span>
                {item.kind === "task" && (
                  <span className="shrink-0 text-xs text-gray-500">{item.sub}</span>
                )}
              </button>
            </li>
          ))}
        </ul>
        <div className="border-t border-gray-100 px-4 py-2 text-[11px] text-gray-400">
          ↑↓ で移動 / Enter で決定 / Esc で閉じる
        </div>
      </div>
    </div>
  );
}
