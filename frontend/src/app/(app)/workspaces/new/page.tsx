"use client";

import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Input } from "@/components/common/Input";
import { Loading } from "@/components/common/Loading";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { fetchCurrentUser } from "@/lib/auth";
import type { CreateWorkspaceBody, Workspace } from "@/types/workspace";

export default function NewWorkspacePage() {
  const router = useRouter();
  const [canCreate, setCanCreate] = useState<boolean | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // ワークスペース作成は System Admin のみ
  useEffect(() => {
    fetchCurrentUser()
      .then((user) => setCanCreate(user?.isSystemAdmin ?? false))
      .catch(() => setCanCreate(false));
  }, []);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      const body: CreateWorkspaceBody = { name, description: description || null };
      const created = await apiFetch<Workspace>("/workspaces", {
        method: "POST",
        body: JSON.stringify(body),
      });
      router.push(`/workspaces/${created.id}`);
    } catch (err) {
      setError(formatApiErrorMessage(err, "ワークスペースの作成に失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  if (canCreate === null) {
    return <Loading />;
  }

  if (!canCreate) {
    return (
      <div className="mx-auto max-w-xl">
        <ErrorMessage message="ワークスペースの作成は System Admin のみ可能です。" />
        <Link href="/projects" className="mt-4 inline-block text-sm text-blue-600 hover:underline">
          ← プロジェクト一覧へ
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-xl">
      <h1 className="mb-6 text-lg font-bold text-gray-900">ワークスペース作成</h1>
      <form onSubmit={handleSubmit} className="space-y-5 rounded-lg bg-white p-6 shadow-sm">
        <Input
          id="name"
          label="ワークスペース名 *"
          required
          maxLength={100}
          value={name}
          onChange={(event) => setName(event.target.value)}
        />
        <div>
          <label htmlFor="description" className="mb-1 block text-sm font-medium text-gray-700">
            説明
          </label>
          <textarea
            id="description"
            rows={3}
            maxLength={500}
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </div>

        {error && <ErrorMessage message={error} />}

        <div className="flex justify-end gap-3">
          <Button type="button" variant="secondary" onClick={() => router.push("/projects")}>
            キャンセル
          </Button>
          <Button type="submit" variant="primary" disabled={isSubmitting}>
            {isSubmitting ? "作成中..." : "作成"}
          </Button>
        </div>
      </form>
    </div>
  );
}
