"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Input } from "@/components/common/Input";
import { Select } from "@/components/common/Select";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import type { CreateInvitationBody } from "@/types/invitation";
import type { WorkspaceRole } from "@/types/workspace";

const ROLE_OPTIONS: { value: WorkspaceRole; label: string }[] = [
  { value: "ADMIN", label: "ADMIN" },
  { value: "MEMBER", label: "MEMBER" },
  { value: "VIEWER", label: "VIEWER" },
];

interface WorkspaceInviteProps {
  workspaceId: number;
}

export function WorkspaceInvite({ workspaceId }: WorkspaceInviteProps) {
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<WorkspaceRole>("MEMBER");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setIsSubmitting(true);
    try {
      const body: CreateInvitationBody = { email: email.trim(), role };
      await apiFetch(`/workspaces/${workspaceId}/invitations`, {
        method: "POST",
        body: JSON.stringify(body),
      });
      setMessage(`${email.trim()} に招待を作成しました。参加用リンクはメールで送信されます。`);
      setEmail("");
      setRole("MEMBER");
    } catch (err) {
      setError(formatApiErrorMessage(err, "招待の作成に失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-wrap items-end gap-3">
      <div className="min-w-[16rem] flex-1">
        <Input
          id="inviteEmail"
          type="email"
          label="メールアドレス"
          required
          value={email}
          onChange={(event) => setEmail(event.target.value)}
        />
      </div>
      <Select
        id="inviteRole"
        label="ロール"
        value={role}
        onChange={(event) => setRole(event.target.value as WorkspaceRole)}
        options={ROLE_OPTIONS}
      />
      <Button type="submit" variant="secondary" disabled={isSubmitting}>
        {isSubmitting ? "作成中..." : "招待する"}
      </Button>

      {message && <p className="w-full text-sm text-green-700">{message}</p>}
      {error && (
        <div className="w-full">
          <ErrorMessage message={error} />
        </div>
      )}
    </form>
  );
}
