"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Input } from "@/components/common/Input";
import { Select } from "@/components/common/Select";
import { formatApiErrorMessage } from "@/lib/api";
import type { ProjectRequestBody, ProjectStatus } from "@/types/project";

const STATUS_OPTIONS: { value: ProjectStatus; label: string }[] = [
  { value: "ACTIVE", label: "ACTIVE" },
  { value: "COMPLETED", label: "COMPLETED" },
  { value: "ARCHIVED", label: "ARCHIVED" },
];

interface ProjectFormProps {
  initialValue: ProjectRequestBody;
  submitLabel: string;
  submittingLabel: string;
  onSubmit: (value: ProjectRequestBody) => Promise<void>;
  onCancel: () => void;
}

export function ProjectForm({
  initialValue,
  submitLabel,
  submittingLabel,
  onSubmit,
  onCancel,
}: ProjectFormProps) {
  const [form, setForm] = useState<ProjectRequestBody>(initialValue);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (form.startDate && form.endDate && form.endDate < form.startDate) {
      setError("終了日は開始日以降の日付を指定してください。");
      return;
    }

    setIsSubmitting(true);
    try {
      // 日付・説明は空文字列だとDateOnly?への変換に失敗するため、未入力ならnullを送る
      await onSubmit({
        ...form,
        description: form.description || null,
        startDate: form.startDate || null,
        endDate: form.endDate || null,
      });
    } catch (err) {
      setError(formatApiErrorMessage(err, "処理に失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <Input
        id="name"
        label="プロジェクト名 *"
        required
        maxLength={200}
        value={form.name}
        onChange={(event) => setForm({ ...form, name: event.target.value })}
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
        id="status"
        label="ステータス"
        value={form.status ?? "ACTIVE"}
        onChange={(event) =>
          setForm({ ...form, status: event.target.value as ProjectStatus })
        }
        options={STATUS_OPTIONS}
      />

      <div className="flex gap-4">
        <div className="flex-1">
          <Input
            id="startDate"
            type="date"
            label="開始日"
            value={form.startDate ?? ""}
            onChange={(event) =>
              setForm({ ...form, startDate: event.target.value })
            }
          />
        </div>
        <div className="flex-1">
          <Input
            id="endDate"
            type="date"
            label="終了日"
            value={form.endDate ?? ""}
            onChange={(event) =>
              setForm({ ...form, endDate: event.target.value })
            }
          />
        </div>
      </div>

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
