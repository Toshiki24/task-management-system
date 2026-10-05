function pad(value: number): string {
  return String(value).padStart(2, "0");
}

/** "2026-09-01" のようなAPIの日付文字列を "2026/09/01" 形式にする(画面設計書§25)。日付のみなのでタイムゾーン変換はしない */
export function formatDate(value: string | null | undefined): string {
  if (!value) {
    return "-";
  }
  // "YYYY-MM-DD" はそのまま年月日を取り出す(new Date だと UTC 深夜扱いで日付がずれるため)
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (match) {
    return `${match[1]}/${match[2]}/${match[3]}`;
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "-";
  }
  return `${date.getFullYear()}/${pad(date.getMonth() + 1)}/${pad(date.getDate())}`;
}

/**
 * APIの日時文字列(UTC)を日本時間(JST)の "2026/09/22 19:00" 形式にする。
 * APIはUTCで保存・返却するため、タイムゾーン指定が無い文字列はUTCとみなす(末尾にZを補う)。
 */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return "-";
  }
  const hasTimezone = /(Z|[+-]\d{2}:?\d{2})$/.test(value);
  const date = new Date(hasTimezone ? value : `${value}Z`);
  if (Number.isNaN(date.getTime())) {
    return "-";
  }
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: "Asia/Tokyo",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(date);
  const get = (type: string) => parts.find((part) => part.type === type)?.value ?? "";
  return `${get("year")}/${get("month")}/${get("day")} ${get("hour")}:${get("minute")}`;
}
