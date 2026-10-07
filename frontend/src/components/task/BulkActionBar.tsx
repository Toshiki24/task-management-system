"use client";

import { useState } from "react";
import { Button } from "@/components/common/Button";
import { Select } from "@/components/common/Select";
import { formatApiErrorMessage } from "@/lib/api";
import type { Label } from "@/types/label";
import type { Member } from "@/types/member";
import type { BulkUpdateBody } from "@/types/task";
import type { WorkflowState } from "@/types/workflow";

interface BulkActionBarProps {
  selectedIds: number[];
  states: WorkflowState[];
  members: Member[];
  labels: Label[];
  onApply: (body: BulkUpdateBody) => Promise<void>;
  onClear: () => void;
}

// 担当セレクトの特別値(通常のユーザー ID と衝突しない文字列)
const ASSIGNEE_NO_CHANGE = "";
const ASSIGNEE_UNASSIGNED = "none";

export function BulkActionBar({ selectedIds, states, members, labels, onApply, onClear }: BulkActionBarProps) {
  const [status, setStatus] = useState("");
  const [assignee, setAssignee] = useState(ASSIGNEE_NO_CHANGE);
  const [addLabelIds, setAddLabelIds] = useState<number[]>([]);
  const [removeLabelIds, setRemoveLabelIds] = useState<number[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function toggle(list: number[], id: number): number[] {
    return list.includes(id) ? list.filter((x) => x !== id) : [...list, id];
  }

  const hasChange =
    status !== "" || assignee !== ASSIGNEE_NO_CHANGE || addLabelIds.length > 0 || removeLabelIds.length > 0;

  function reset() {
    setStatus("");
    setAssignee(ASSIGNEE_NO_CHANGE);
    setAddLabelIds([]);
    setRemoveLabelIds([]);
  }

  async function handleApply() {
    if (!hasChange || selectedIds.length === 0) return;
    const body: BulkUpdateBody = { taskIds: selectedIds };
    if (status !== "") body.status = status;
    if (assignee !== ASSIGNEE_NO_CHANGE) {
      body.setAssignee = true;
      body.assigneeId = assignee === ASSIGNEE_UNASSIGNED ? null : Number(assignee);
    }
    if (addLabelIds.length > 0) body.addLabelIds = addLabelIds;
    if (removeLabelIds.length > 0) body.removeLabelIds = removeLabelIds;

    setIsSubmitting(true);
    setError(null);
    try {
      await onApply(body);
      reset();
    } catch (err) {
      setError(formatApiErrorMessage(err, "一括更新に失敗しました。"));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="mb-3 rounded-lg border border-blue-200 bg-blue-50 p-3">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-sm font-medium text-blue-900">{selectedIds.length} 件選択中</span>
        <button type="button" onClick={onClear} className="text-xs text-blue-700 hover:underline">
          選択を解除
        </button>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <div className="w-40">
          <Select
            id="bulkStatus"
            label="状態"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
            options={[{ value: "", label: "変更しない" }, ...states.map((s) => ({ value: s.key, label: s.name }))]}
          />
        </div>
        <div className="w-40">
          <Select
            id="bulkAssignee"
            label="担当者"
            value={assignee}
            onChange={(e) => setAssignee(e.target.value)}
            options={[
              { value: ASSIGNEE_NO_CHANGE, label: "変更しない" },
              { value: ASSIGNEE_UNASSIGNED, label: "未割り当て" },
              ...members.map((m) => ({ value: String(m.userId), label: m.name })),
            ]}
          />
        </div>
        <Button type="button" variant="primary" onClick={handleApply} disabled={!hasChange || isSubmitting}>
          {isSubmitting ? "適用中..." : "適用"}
        </Button>
      </div>

      {labels.length > 0 && (
        <div className="mt-3 space-y-2">
          <LabelChips title="付与するラベル" labels={labels} selected={addLabelIds} onToggle={(id) => setAddLabelIds((v) => toggle(v, id))} />
          <LabelChips title="外すラベル" labels={labels} selected={removeLabelIds} onToggle={(id) => setRemoveLabelIds((v) => toggle(v, id))} />
        </div>
      )}

      {error && <p className="mt-2 text-sm text-red-600">{error}</p>}
    </div>
  );
}

function LabelChips({
  title,
  labels,
  selected,
  onToggle,
}: {
  title: string;
  labels: Label[];
  selected: number[];
  onToggle: (id: number) => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <span className="text-xs font-medium text-gray-600">{title}</span>
      {labels.map((label) => {
        const on = selected.includes(label.id);
        return (
          <button
            key={label.id}
            type="button"
            aria-pressed={on}
            onClick={() => onToggle(label.id)}
            className={`flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs ${
              on ? "border-blue-500 bg-white" : "border-gray-300 bg-white/60 text-gray-600"
            }`}
          >
            <span
              aria-hidden
              className="inline-block h-2.5 w-2.5 rounded-full"
              style={{ backgroundColor: label.color ?? "#9ca3af" }}
            />
            {label.name}
          </button>
        );
      })}
    </div>
  );
}
