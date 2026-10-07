"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Input } from "@/components/common/Input";
import { Select } from "@/components/common/Select";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import { PRIORITY_LABELS } from "@/lib/taskLabels";
import type { Label } from "@/types/label";
import type { Member } from "@/types/member";
import type { Project } from "@/types/project";
import type { TaskPriority, TaskRequestBody, TaskStatus } from "@/types/task";
import type { WorkflowState } from "@/types/workflow";

const FALLBACK_STATUS_OPTIONS: { value: TaskStatus; label: string }[] = [
  { value: "TODO", label: "未着手" },
  { value: "IN_PROGRESS", label: "対応中" },
  { value: "DONE", label: "完了" },
];

const PRIORITY_OPTIONS: { value: TaskPriority; label: string }[] = [
  { value: "HIGH", label: PRIORITY_LABELS.HIGH },
  { value: "MEDIUM", label: PRIORITY_LABELS.MEDIUM },
  { value: "LOW", label: PRIORITY_LABELS.LOW },
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
  const [labels, setLabels] = useState<Label[]>([]);
  const [statusOptions, setStatusOptions] =
    useState<{ value: string; label: string }[]>(FALLBACK_STATUS_OPTIONS);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    apiFetch<Member[]>(`/projects/${projectId}/members`)
      .then(setMembers)
      .catch(() => setMembers([]));

    // プロジェクトの所属ワークスペースのワークフロー状態・ラベルを取得する
    apiFetch<Project>(`/projects/${projectId}`)
      .then(async (project) => {
        const [states, wsLabels] = await Promise.all([
          apiFetch<WorkflowState[]>(`/workspaces/${project.workspaceId}/workflow-states`),
          apiFetch<Label[]>(`/workspaces/${project.workspaceId}/labels`),
        ]);
        if (states.length > 0) {
          setStatusOptions(states.map((s) => ({ value: s.key, label: s.name })));
        }
        setLabels(wsLabels);
      })
      .catch(() => {
        // 取得失敗時は既定のステータス候補・ラベルなしで続行する
      });
  }, [projectId]);

  function toggleLabel(labelId: number) {
    const current = form.labelIds ?? [];
    setForm({
      ...form,
      labelIds: current.includes(labelId)
        ? current.filter((id) => id !== labelId)
        : [...current, labelId],
    });
  }

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
      setError(formatApiErrorMessage(err, "処理に失敗しました。"));
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
            value={form.status ?? statusOptions[0]?.value ?? "TODO"}
            onChange={(event) =>
              setForm({ ...form, status: event.target.value as TaskStatus })
            }
            options={statusOptions}
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

      <div className="flex gap-4">
        <div className="flex-1">
          <Input
            id="dueDate"
            type="date"
            label="期限"
            value={form.dueDate ?? ""}
            onChange={(event) => setForm({ ...form, dueDate: event.target.value })}
          />
        </div>
        <div className="flex-1">
          <Input
            id="estimatePoints"
            type="number"
            min={0}
            label="見積(ポイント)"
            value={form.estimatePoints ?? ""}
            onChange={(event) =>
              setForm({
                ...form,
                estimatePoints: event.target.value === "" ? null : Number(event.target.value),
              })
            }
          />
        </div>
      </div>

      {labels.length > 0 && (
        <div>
          <span className="mb-1 block text-sm font-medium text-gray-700">ラベル</span>
          <div className="flex flex-wrap gap-2">
            {labels.map((label) => {
              const selected = (form.labelIds ?? []).includes(label.id);
              return (
                <label
                  key={label.id}
                  className={`flex cursor-pointer items-center gap-1.5 rounded-full border px-3 py-1 text-sm ${
                    selected ? "border-blue-500 bg-blue-50" : "border-gray-300"
                  }`}
                >
                  <input
                    type="checkbox"
                    className="sr-only"
                    checked={selected}
                    onChange={() => toggleLabel(label.id)}
                  />
                  <span
                    aria-hidden
                    className="inline-block h-3 w-3 rounded-full"
                    style={{ backgroundColor: label.color ?? "#9ca3af" }}
                  />
                  {label.name}
                </label>
              );
            })}
          </div>
        </div>
      )}

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
