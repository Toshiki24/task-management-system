"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Input } from "@/components/common/Input";
import { Select } from "@/components/common/Select";
import { ApiError, apiFetch } from "@/lib/api";
import type { Member } from "@/types/member";
import type { TaskPriority, TaskRequestBody, TaskStatus } from "@/types/task";

const STATUS_OPTIONS: { value: TaskStatus; label: string }[] = [
  { value: "TODO", label: "TODO" },
  { value: "IN_PROGRESS", label: "IN_PROGRESS" },
  { value: "DONE", label: "DONE" },
];

const PRIORITY_OPTIONS: { value: TaskPriority; label: string }[] = [
  { value: "LOW", label: "LOW" },
  { value: "MEDIUM", label: "MEDIUM" },
  { value: "HIGH", label: "HIGH" },
];

interface TaskFormProps {
  projectId: string | number;
  initialValue: TaskRequestBody;
  submitLabel: string;
  submittingLabel: string;
  onSubmit: (value: TaskRequestBody) => Promise<void>;
  onCancel: () => void;
}

export function TaskForm({
  projectId,
  initialValue,
  submitLabel,
  submittingLabel,
  onSubmit,
  onCancel,
}: TaskFormProps) {
  const [form, setForm] = useState<TaskRequestBody>(initialValue);
  const [members, setMembers] = useState<Member[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    apiFetch<Member[]>(`/projects/${projectId}/members`)
      .then(setMembers)
      .catch(() => setMembers([]));
  }, [projectId]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      // 日付は空文字列だとDateOnly?への変換に失敗するため、未入力ならnullを送る
      await onSubmit({
        ...form,
        description: form.description || null,
        dueDate: form.dueDate || null,
      });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "処理に失敗しました。");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <Input
        id="title"
        label="タイトル *"
        required
        maxLength={200}
        value={form.title}
        onChange={(event) => setForm({ ...form, title: event.target.value })}
      />

      <div>
        <label
          htmlFor="description"
          className="mb-1 block text-sm font-medium text-gray-700"
        >
          説明
        </label>
        <textarea
          id="description"
          rows={3}
          value={form.description ?? ""}
          onChange={(event) =>
            setForm({ ...form, description: event.target.value })
          }
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
      </div>

      <Select
        id="assigneeId"
        label="担当者"
        value={form.assigneeId ? String(form.assigneeId) : ""}
        onChange={(event) =>
          setForm({
            ...form,
            assigneeId: event.target.value ? Number(event.target.value) : null,
          })
        }
        options={[
          { value: "", label: "未割り当て" },
          ...(members ?? []).map((member) => ({
            value: String(member.userId),
            label: member.name,
          })),
        ]}
      />

      <div className="flex gap-4">
        <div className="flex-1">
          <Select
            id="status"
            label="ステータス"
            value={form.status ?? "TODO"}
            onChange={(event) =>
              setForm({ ...form, status: event.target.value as TaskStatus })
            }
            options={STATUS_OPTIONS}
          />
        </div>
        <div className="flex-1">
          <Select
            id="priority"
            label="優先度"
            value={form.priority ?? "MEDIUM"}
            onChange={(event) =>
              setForm({
                ...form,
                priority: event.target.value as TaskPriority,
              })
            }
            options={PRIORITY_OPTIONS}
          />
        </div>
      </div>

      <Input
        id="dueDate"
        type="date"
        label="期限"
        value={form.dueDate ?? ""}
        onChange={(event) => setForm({ ...form, dueDate: event.target.value })}
      />

      {error && <ErrorMessage message={error} />}

      <div className="flex justify-end gap-3">
        <Button type="button" variant="secondary" onClick={onCancel}>
          キャンセル
        </Button>
        <Button type="submit" variant="primary" disabled={isSubmitting}>
          {isSubmitting ? submittingLabel : submitLabel}
        </Button>
      </div>
    </form>
  );
}
