"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Select } from "@/components/common/Select";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import type { AdminWorkspace } from "@/types/admin";

interface AdminCreateUserProps {
  /** 作成成功時に呼ぶ(ユーザー一覧の再取得用)。 */
  onCreated: () => void;
}

const ROLE_OPTIONS = [
  { value: "MEMBER", label: "メンバー" },
  { value: "ADMIN", label: "管理者" },
  { value: "VIEWER", label: "閲覧者" },
];

/**
 * System Admin による手動ユーザー作成(メール招待が使えない運用向け。M5 §8)。
 * メール・パスワードでユーザーを作成し、指定ワークスペースにロール付きで追加する。
 */
export function AdminCreateUser({ onCreated }: AdminCreateUserProps) {
  const [workspaces, setWorkspaces] = useState<AdminWorkspace[]>([]);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [name, setName] = useState("");
  const [workspaceId, setWorkspaceId] = useState("");
  const [role, setRole] = useState("MEMBER");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    apiFetch<AdminWorkspace[]>("/system/workspaces")
      .then((list) => {
        setWorkspaces(list);
        if (list.length > 0) setWorkspaceId(String(list[0].id));
      })
      .catch(() => setError("ワークスペース一覧の取得に失敗しました。"));
  }, []);

  async function handleSubmit() {
    setError(null);
    setMessage(null);
    if (!workspaceId) {
      setError("ワークスペースを選択してください。");
      return;
    }
    setIsSubmitting(true);
    try {
      const created = await apiFetch<{ email: string }>("/system/users", {
        method: "POST",
        body: JSON.stringify({
          email: email.trim(),
          password,
          name: name.trim() || null,
          workspaceId: Number(workspaceId),
          role,
        }),
      });
      setMessage(`${created.email} を作成しました。`);
      setEmail("");
      setPassword("");
      setName("");
      onCreated();
    } catch (err) {
      setError(formatApiErrorMessage(err, "ユーザーの作成に失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section>
      <h2 className="mb-3 text-base font-bold text-gray-900">ユーザーを手動で作成</h2>
      <p className="mb-3 text-sm text-gray-600">
        メール送信（SMTP）を設定していない場合でも、ここでメールとパスワードを指定してユーザーを作成し、ワークスペースに追加できます。
      </p>

      {error && <ErrorMessage message={error} />}
      {message && <p className="mb-2 text-sm text-green-700">{message}</p>}

      <div className="grid max-w-xl grid-cols-1 gap-3 sm:grid-cols-2">
        <div>
          <label htmlFor="cu-email" className="mb-1 block text-sm font-medium text-gray-700">メールアドレス</label>
          <input
            id="cu-email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
          />
        </div>
        <div>
          <label htmlFor="cu-password" className="mb-1 block text-sm font-medium text-gray-700">パスワード（8文字以上）</label>
          <input
            id="cu-password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
          />
        </div>
        <div>
          <label htmlFor="cu-name" className="mb-1 block text-sm font-medium text-gray-700">名前（任意）</label>
          <input
            id="cu-name"
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="未入力ならメール名を使用"
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900"
          />
        </div>
        <Select
          id="cu-workspace"
          label="ワークスペース"
          value={workspaceId}
          onChange={(e) => setWorkspaceId(e.target.value)}
          options={workspaces.map((w) => ({ value: String(w.id), label: w.name }))}
        />
        <Select
          id="cu-role"
          label="ロール"
          value={role}
          onChange={(e) => setRole(e.target.value)}
          options={ROLE_OPTIONS}
        />
      </div>

      <div className="mt-3">
        <Button
          type="button"
          onClick={handleSubmit}
          disabled={isSubmitting || !email.trim() || !password || !workspaceId}
        >
          {isSubmitting ? "作成中..." : "ユーザーを作成"}
        </Button>
      </div>
    </section>
  );
}
