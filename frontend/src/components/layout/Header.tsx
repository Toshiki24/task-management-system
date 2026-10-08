import { NotificationBell } from "@/components/layout/NotificationBell";

interface HeaderProps {
  userName?: string;
  onOpenCommand?: () => void;
}

export function Header({ userName, onOpenCommand }: HeaderProps) {
  return (
    <header className="flex items-center justify-between border-b border-gray-200 bg-white px-6 py-3">
      <span className="font-bold text-gray-900">案件・タスク管理システム</span>
      <div className="flex items-center gap-4">
        <NotificationBell />
        {onOpenCommand && (
          <button
            type="button"
            onClick={onOpenCommand}
            aria-label="コマンドパレットを開く"
            className="flex items-center gap-2 rounded-md border border-gray-300 px-2.5 py-1 text-xs text-gray-500 hover:bg-gray-50"
          >
            <span>検索・コマンド</span>
            <kbd className="rounded bg-gray-100 px-1.5 py-0.5 font-sans text-[10px] text-gray-500">⌘K</kbd>
          </button>
        )}
        <span className="text-sm text-gray-600">{userName} ▼</span>
      </div>
    </header>
  );
}
