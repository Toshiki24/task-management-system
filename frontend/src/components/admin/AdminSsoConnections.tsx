"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Select } from "@/components/common/Select";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import type { AdminWorkspace } from "@/types/admin";
import type { SsoConnection } from "@/types/sso";

const ROLE_OPTIONS = [
  { value: "MEMBER", label: "メンバー" },
  { value: "ADMIN", label: "管理者" },
  { value: "VIEWER", label: "閲覧者" },
];

const INPUT_CLASS = "w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900";

/** SSO(OIDC)接続の管理(System Admin。M5 §7)。 */
export function AdminSsoConnections() {
  const [connections, setConnections] = useState<SsoConnection[] | null>(null);
  const [workspaces, setWorkspaces] = useState<AdminWorkspace[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // 作成フォーム
  const [name, setName] = useState("");
  const [issuer, setIssuer] = useState("");
  const [clientId, setClientId] = useState("");
  const [secretRef, setSecretRef] = useState("");
  const [scopes, setScopes] = useState("openid email profile");
  const [autoProvision, setAutoProvision] = useState(false);
  const [provisionWorkspaceId, setProvisionWorkspaceId] = useState("");
  const [provisionRole, setProvisionRole] = useState("MEMBER");

  const callbackUrl =
    typeof window !== "undefined" ? `${window.location.origin}/api/bff/auth/sso/callback` : "";

  async function reload() {
    setConnections(await apiFetch<SsoConnection[]>("/system/sso-connections"));
  }

  useEffect(() => {
    reload().catch(() => setError("SSO 接続の取得に失敗しました。"));
    apiFetch<AdminWorkspace[]>("/system/workspaces")
      .then(setWorkspaces)
      .catch(() => {});
  }, []);

  async function handleCreate() {
    setError(null);
    setMessage(null);
    setBusy(true);
    try {
      await apiFetch<SsoConnection>("/system/sso-connections", {
        method: "POST",
        body: JSON.stringify({
          name: name.trim(),
          issuer: issuer.trim(),
          clientId: clientId.trim(),
          secretRef: secretRef.trim() || null,
          scopes: scopes.trim() || null,
          autoProvision,
          provisionWorkspaceId: autoProvision && provisionWorkspaceId ? Number(provisionWorkspaceId) : null,
          provisionRole: autoProvision ? provisionRole : null,
        }),
      });
      setMessage(`${name.trim()} を追加しました。`);
      setName("");
      setIssuer("");
      setClientId("");
      setSecretRef("");
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "SSO 接続の作成に失敗しました。"));
    } finally {
      setBusy(false);
    }
  }

  async function toggleEnabled(connection: SsoConnection) {
    setError(null);
    setBusy(true);
    try {
      await apiFetch<SsoConnection>(`/system/sso-connections/${connection.id}`, {
        method: "PUT",
        body: JSON.stringify({
          name: connection.name,
          issuer: connection.issuer,
          clientId: connection.clientId,
          secretRef: null, // 変更しない
          scopes: connection.scopes,
          autoProvision: connection.autoProvision,
          provisionWorkspaceId: connection.provisionWorkspaceId,
          provisionRole: connection.provisionRole,
          isEnabled: !connection.isEnabled,
        }),
      });
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "状態の変更に失敗しました。"));
    } finally {
      setBusy(false);
    }
  }

  async function remove(connection: SsoConnection) {
    setError(null);
    setBusy(true);
    try {
      await apiFetch<void>(`/system/sso-connections/${connection.id}`, { method: "DELETE" });
      await reload();
    } catch (err) {
      setError(formatApiErrorMessage(err, "削除に失敗しました。"));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section>
      <h2 className="mb-3 text-base font-bold text-gray-900">SSO（OIDC）接続</h2>
      <p className="mb-3 text-sm text-gray-600">
        外部 IdP（Google Workspace・Microsoft Entra ID・Okta 等）で SSO ログインできるようにします。
        IdP 側には、リダイレクト URI として <code className="rounded bg-gray-100 px-1">{callbackUrl}</code> を登録してください。
        クライアントシークレットは値を保存せず、シークレットストアの参照名で指定します（本番は環境変数/Secrets Manager）。
      </p>

      {error && <ErrorMessage message={error} />}
      {message && <p className="mb-2 text-sm text-green-700">{message}</p>}

      {!connections && <p className="text-sm text-gray-500">読み込み中...</p>}
      {connections && connections.length > 0 && (
        <table className="mb-6 w-full border-collapse text-sm">
          <thead>
            <tr className="border-b border-gray-200 text-left text-gray-500">
              <th className="py-2 pr-3">名称</th>
              <th className="py-2 pr-3">Issuer</th>
              <th className="py-2 pr-3">自動作成</th>
              <th className="py-2 pr-3">状態</th>
              <th className="py-2 pr-3">操作</th>
            </tr>
          </thead>
          <tbody>
            {connections.map((c) => (
              <tr key={c.id} className="border-b border-gray-100" data-testid={`sso-row-${c.id}`}>
                <td className="py-2 pr-3 text-gray-900">{c.name}</td>
                <td className="py-2 pr-3 text-gray-600">{c.issuer}</td>
                <td className="py-2 pr-3 text-gray-600">{c.autoProvision ? "有効" : "無効"}</td>
                <td className="py-2 pr-3">
                  {c.isEnabled ? (
                    <span className="text-xs text-green-700">有効</span>
                  ) : (
                    <span className="text-xs text-gray-400">無効</span>
                  )}
                </td>
                <td className="py-2 pr-3">
                  <div className="flex gap-2">
                    <Button type="button" variant="secondary" disabled={busy} onClick={() => toggleEnabled(c)}>
                      {c.isEnabled ? "無効化" : "有効化"}
                    </Button>
                    <Button type="button" variant="danger" disabled={busy} onClick={() => remove(c)}>
                      削除
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <div className="rounded-md border border-gray-200 p-4">
        <h3 className="mb-3 text-sm font-bold text-gray-900">接続を追加</h3>
        <div className="grid max-w-xl grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label htmlFor="sso-name" className="mb-1 block text-sm font-medium text-gray-700">名称</label>
            <input id="sso-name" value={name} onChange={(e) => setName(e.target.value)}
              placeholder="Google Workspace" className={INPUT_CLASS} />
          </div>
          <div>
            <label htmlFor="sso-issuer" className="mb-1 block text-sm font-medium text-gray-700">Issuer URL</label>
            <input id="sso-issuer" value={issuer} onChange={(e) => setIssuer(e.target.value)}
              placeholder="https://accounts.google.com" className={INPUT_CLASS} />
          </div>
          <div>
            <label htmlFor="sso-client" className="mb-1 block text-sm font-medium text-gray-700">クライアント ID</label>
            <input id="sso-client" value={clientId} onChange={(e) => setClientId(e.target.value)} className={INPUT_CLASS} />
          </div>
          <div>
            <label htmlFor="sso-secret" className="mb-1 block text-sm font-medium text-gray-700">
              シークレット参照名（任意）
            </label>
            <input id="sso-secret" value={secretRef} onChange={(e) => setSecretRef(e.target.value)}
              placeholder="例: google-oidc" className={INPUT_CLASS} />
          </div>
          <div>
            <label htmlFor="sso-scopes" className="mb-1 block text-sm font-medium text-gray-700">スコープ</label>
            <input id="sso-scopes" value={scopes} onChange={(e) => setScopes(e.target.value)} className={INPUT_CLASS} />
          </div>
        </div>

        <label className="mt-3 flex items-center gap-2 text-sm text-gray-800">
          <input type="checkbox" checked={autoProvision} onChange={(e) => setAutoProvision(e.target.checked)}
            className="h-4 w-4 rounded border-gray-300" />
          未登録メールのユーザーを自動作成する
        </label>

        {autoProvision && (
          <div className="mt-3 grid max-w-xl grid-cols-1 gap-3 sm:grid-cols-2">
            <Select
              id="sso-ws"
              label="追加先ワークスペース"
              value={provisionWorkspaceId}
              onChange={(e) => setProvisionWorkspaceId(e.target.value)}
              options={[
                { value: "", label: "（追加しない）" },
                ...workspaces.map((w) => ({ value: String(w.id), label: w.name })),
              ]}
            />
            <Select
              id="sso-role"
              label="ロール"
              value={provisionRole}
              onChange={(e) => setProvisionRole(e.target.value)}
              options={ROLE_OPTIONS}
            />
          </div>
        )}

        <div className="mt-3">
          <Button type="button" onClick={handleCreate}
            disabled={busy || !name.trim() || !issuer.trim() || !clientId.trim()}>
            {busy ? "処理中..." : "接続を追加"}
          </Button>
        </div>
      </div>
    </section>
  );
}
