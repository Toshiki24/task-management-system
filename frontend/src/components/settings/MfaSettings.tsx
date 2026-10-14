"use client";

import { useEffect, useState } from "react";
import QRCode from "qrcode";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import type { MfaEnrollment, MfaRecoveryCodes, MfaStatus } from "@/types/mfa";

/** リカバリコードの一覧表示(発行直後のみ平文で表示する)。 */
function RecoveryCodeList({ codes }: { codes: string[] }) {
  return (
    <div className="mt-4 rounded-md border border-amber-300 bg-amber-50 p-4">
      <p className="text-sm font-medium text-amber-800">
        リカバリコード(各コードは1回のみ使用可。安全な場所に保管してください)
      </p>
      <ul className="mt-2 grid grid-cols-2 gap-1 font-mono text-sm text-gray-800" data-testid="recovery-codes">
        {codes.map((code) => (
          <li key={code}>{code}</li>
        ))}
      </ul>
    </div>
  );
}

/** 本人の MFA(TOTP)設定(M5 §6)。 */
export function MfaSettings() {
  const [status, setStatus] = useState<MfaStatus | null>(null);
  const [enrollment, setEnrollment] = useState<MfaEnrollment | null>(null);
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    apiFetch<MfaStatus>("/mfa")
      .then(setStatus)
      .catch(() => setError("MFA の状態を取得できませんでした。"));
  }, []);

  // otpauth URI を QR コード(data URL)に変換する
  useEffect(() => {
    if (!enrollment) {
      setQrDataUrl(null);
      return;
    }
    QRCode.toDataURL(enrollment.otpauthUri, { width: 200, margin: 1 })
      .then(setQrDataUrl)
      .catch(() => setQrDataUrl(null));
  }, [enrollment]);

  async function begin() {
    setError(null);
    setBusy(true);
    try {
      const data = await apiFetch<MfaEnrollment>("/mfa/enroll", { method: "POST" });
      setEnrollment(data);
      setRecoveryCodes(null);
      setCode("");
    } catch (err) {
      setError(formatApiErrorMessage(err, "MFA の有効化を開始できませんでした。"));
    } finally {
      setBusy(false);
    }
  }

  async function confirm() {
    setError(null);
    setBusy(true);
    try {
      const data = await apiFetch<MfaRecoveryCodes>("/mfa/confirm", {
        method: "POST",
        body: JSON.stringify({ code: code.trim() }),
      });
      setRecoveryCodes(data.recoveryCodes);
      setEnrollment(null);
      setStatus({ enabled: true });
      setCode("");
    } catch (err) {
      setError(formatApiErrorMessage(err, "コードを確認できませんでした。"));
    } finally {
      setBusy(false);
    }
  }

  async function disable() {
    setError(null);
    setBusy(true);
    try {
      await apiFetch<void>("/mfa/disable", {
        method: "POST",
        body: JSON.stringify({ code: code.trim() }),
      });
      setStatus({ enabled: false });
      setRecoveryCodes(null);
      setCode("");
    } catch (err) {
      setError(formatApiErrorMessage(err, "MFA を無効化できませんでした。"));
    } finally {
      setBusy(false);
    }
  }

  async function regenerate() {
    setError(null);
    setBusy(true);
    try {
      const data = await apiFetch<MfaRecoveryCodes>("/mfa/recovery-codes", {
        method: "POST",
        body: JSON.stringify({ code: code.trim() }),
      });
      setRecoveryCodes(data.recoveryCodes);
      setCode("");
    } catch (err) {
      setError(formatApiErrorMessage(err, "リカバリコードを再発行できませんでした。"));
    } finally {
      setBusy(false);
    }
  }

  if (!status) {
    return <p className="text-sm text-gray-500">読み込み中...</p>;
  }

  return (
    <section className="space-y-4">
      <div className="flex items-center gap-3">
        <h2 className="text-base font-bold text-gray-900">二要素認証(MFA）</h2>
        <span
          className={`rounded px-2 py-0.5 text-xs font-medium ${
            status.enabled ? "bg-green-100 text-green-700" : "bg-gray-100 text-gray-600"
          }`}
        >
          {status.enabled ? "有効" : "無効"}
        </span>
      </div>
      <p className="text-sm text-gray-600">
        認証アプリ(Google Authenticator 等)でワンタイムコードを使い、ログインを保護します。
      </p>

      {error && <ErrorMessage message={error} />}

      {/* 無効 & 登録未開始 */}
      {!status.enabled && !enrollment && (
        <Button type="button" onClick={begin} disabled={busy}>
          MFA を有効にする
        </Button>
      )}

      {/* 登録中(QR 表示 → コード確認) */}
      {!status.enabled && enrollment && (
        <div className="space-y-3 rounded-md border border-gray-200 p-4">
          <p className="text-sm text-gray-700">
            認証アプリで QR コードを読み取るか、次のキーを手動で登録してください。
          </p>
          {qrDataUrl && (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={qrDataUrl} alt="MFA 設定用 QR コード" className="h-48 w-48" />
          )}
          <p className="break-all font-mono text-sm text-gray-800" data-testid="mfa-secret">
            {enrollment.secret}
          </p>
          <div>
            <label htmlFor="enroll-code" className="mb-1 block text-sm font-medium text-gray-700">
              認証アプリに表示されたコード
            </label>
            <input
              id="enroll-code"
              type="text"
              inputMode="numeric"
              autoComplete="one-time-code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              placeholder="123456"
              className="w-40 rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
            />
          </div>
          <div className="flex gap-3">
            <Button type="button" onClick={confirm} disabled={busy || !code.trim()}>
              確認して有効化
            </Button>
            <Button type="button" variant="secondary" onClick={() => setEnrollment(null)} disabled={busy}>
              キャンセル
            </Button>
          </div>
        </div>
      )}

      {/* 有効時: 無効化・リカバリコード再発行 */}
      {status.enabled && (
        <div className="space-y-3 rounded-md border border-gray-200 p-4">
          <label htmlFor="manage-code" className="block text-sm font-medium text-gray-700">
            操作には認証コード(またはリカバリコード)が必要です
          </label>
          <input
            id="manage-code"
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            placeholder="123456"
            className="w-40 rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
          />
          <div className="flex gap-3">
            <Button type="button" variant="secondary" onClick={regenerate} disabled={busy || !code.trim()}>
              リカバリコードを再発行
            </Button>
            <Button type="button" variant="danger" onClick={disable} disabled={busy || !code.trim()}>
              MFA を無効にする
            </Button>
          </div>
        </div>
      )}

      {recoveryCodes && <RecoveryCodeList codes={recoveryCodes} />}
    </section>
  );
}
