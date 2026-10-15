"use client";

import { useEffect, useState } from "react";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import type { NotificationSettings as Settings } from "@/types/notifications";

/**
 * 本人のメール通知設定(M5 §8)。
 * メンション・コメント・状態変更・担当割り当て・期限などの通知をメールで受け取るかを切り替える。
 * SMTP 未設定の環境では送信されないため、その場合は補足を表示する。
 */
export function NotificationSettings() {
  const [emailEnabled, setEmailEnabled] = useState<boolean | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    apiFetch<Settings>("/users/me/notification-settings")
      .then((s) => setEmailEnabled(s.emailEnabled))
      .catch(() => setError("通知設定を取得できませんでした。"));
  }, []);

  async function toggle(next: boolean) {
    setError(null);
    setMessage(null);
    setBusy(true);
    try {
      const updated = await apiFetch<Settings>("/users/me/notification-settings", {
        method: "PUT",
        body: JSON.stringify({ emailEnabled: next }),
      });
      setEmailEnabled(updated.emailEnabled);
      setMessage("通知設定を保存しました。");
    } catch (err) {
      setError(formatApiErrorMessage(err, "通知設定を保存できませんでした。"));
    } finally {
      setBusy(false);
    }
  }

  if (emailEnabled === null && !error) {
    return <p className="text-sm text-gray-500">読み込み中...</p>;
  }

  return (
    <section className="space-y-4">
      <h2 className="text-base font-bold text-gray-900">通知設定</h2>
      <p className="text-sm text-gray-600">
        メンション・コメント・状態変更・担当割り当て・期限などの通知をメールで受け取るかを設定します。
        （メール送信（SMTP）が未設定の環境では、設定に関わらずメールは送信されません。アプリ内通知は常に表示されます。）
      </p>

      {error && <ErrorMessage message={error} />}
      {message && <p className="text-sm text-green-700">{message}</p>}

      {emailEnabled !== null && (
        <label className="flex items-center gap-3 text-sm text-gray-800">
          <input
            type="checkbox"
            checked={emailEnabled}
            disabled={busy}
            onChange={(e) => toggle(e.target.checked)}
            className="h-4 w-4 rounded border-gray-300"
            data-testid="email-notifications-toggle"
          />
          メール通知を受け取る
        </label>
      )}
    </section>
  );
}
