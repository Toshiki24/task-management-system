interface HeaderProps {
  userName?: string;
}

export function Header({ userName }: HeaderProps) {
  return (
    <header className="flex items-center justify-between border-b border-gray-200 bg-white px-6 py-3">
      <span className="font-bold text-gray-900">案件・タスク管理システム</span>
      <span className="text-sm text-gray-600">{userName} ▼</span>
    </header>
  );
}
