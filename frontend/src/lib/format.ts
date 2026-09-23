function pad(value: number): string {
  return String(value).padStart(2, "0");
}

/** "2026-09-01" のようなAPIの日付文字列を "2026/09/01" 形式にする(画面設計書§25) */
export function formatDate(value: string | null | undefined): string {
  if (!value) {
    return "-";
  }
  const date = new Date(value);
  return `${date.getFullYear()}/${pad(date.getMonth() + 1)}/${pad(date.getDate())}`;
}

/** "2026-09-22T10:00:00" のようなAPIの日時文字列を "2026/09/22 10:00" 形式にする */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return "-";
  }
  const date = new Date(value);
  return `${formatDate(value)} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
