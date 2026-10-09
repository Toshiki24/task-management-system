import Link from "next/link";

interface SidebarProps {
  onLogout: () => void;
  /** System Admin のときだけ管理コンソールへの導線を出す。 */
  isSystemAdmin?: boolean;
}

export function Sidebar({ onLogout, isSystemAdmin = false }: SidebarProps) {
  return (
    <nav className="w-48 shrink-0 border-r border-gray-200 bg-white p-4">
      <Link
        href="/projects"
        className="block rounded-md px-3 py-2 text-sm text-gray-700 hover:bg-gray-100"
      >
        プロジェクト
      </Link>
      <Link
        href="/me/tasks"
        className="block rounded-md px-3 py-2 text-sm text-gray-700 hover:bg-gray-100"
      >
        マイタスク
      </Link>
      {isSystemAdmin && (
        <Link
          href="/admin"
          className="block rounded-md px-3 py-2 text-sm text-gray-700 hover:bg-gray-100"
        >
          管理コンソール
        </Link>
      )}
      <button
        type="button"
        onClick={onLogout}
        className="block w-full rounded-md px-3 py-2 text-left text-sm text-gray-700 hover:bg-gray-100"
      >
        ログアウト
      </button>
    </nav>
  );
}
