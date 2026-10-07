"use client";

import { Select } from "@/components/common/Select";
import { PRIORITY_LABELS } from "@/lib/taskLabels";
import type { Label } from "@/types/label";
import type { Member } from "@/types/member";
import type { WorkflowState } from "@/types/workflow";

export interface TaskFilters {
  keyword: string;
  status: string[];
  assigneeId: string;
  priority: string[];
  labelId: number[];
  sort: string;
}

export const EMPTY_FILTERS: TaskFilters = {
  keyword: "",
  status: [],
  assigneeId: "",
  priority: [],
  labelId: [],
  sort: "",
};

/** フィルタ条件をタスク一覧APIのクエリ文字列に変換する(空の条件は付けない)。 */
export function buildTaskQuery(f: TaskFilters): string {
  const params = new URLSearchParams();
  if (f.keyword.trim()) params.set("keyword", f.keyword.trim());
  for (const s of f.status) params.append("status", s);
  for (const p of f.priority) params.append("priority", p);
  for (const id of f.labelId) params.append("labelId", String(id));
  if (f.assigneeId) params.set("assigneeId", f.assigneeId);
  if (f.sort) params.set("sort", f.sort);
  const qs = params.toString();
  return qs ? `?${qs}` : "";
}

const PRIORITIES = [
  { value: "HIGH", label: PRIORITY_LABELS.HIGH },
  { value: "MEDIUM", label: PRIORITY_LABELS.MEDIUM },
  { value: "LOW", label: PRIORITY_LABELS.LOW },
];

const SORT_OPTIONS = [
  { value: "", label: "既定" },
  { value: "dueDate", label: "期限が近い順" },
  { value: "-dueDate", label: "期限が遠い順" },
  { value: "-priority", label: "優先度が高い順" },
  { value: "title", label: "タイトル昇順" },
  { value: "-updatedAt", label: "更新が新しい順" },
];

interface TaskFilterBarProps {
  states: WorkflowState[];
  labels: Label[];
  members: Member[];
  value: TaskFilters;
  onChange: (next: TaskFilters) => void;
  /** 現在のユーザーID。担当者候補で「自分」と二重に並ばないよう、本人はメンバー一覧から除外する。 */
  currentUserId?: number | null;
}

function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={`rounded-full border px-2.5 py-0.5 text-xs ${
        active ? "border-blue-500 bg-blue-50 text-blue-700" : "border-gray-300 text-gray-600"
      }`}
    >
      {children}
    </button>
  );
}

export function TaskFilterBar({ states, labels, members, value, onChange, currentUserId }: TaskFilterBarProps) {
  function toggle<T>(list: T[], item: T): T[] {
    return list.includes(item) ? list.filter((x) => x !== item) : [...list, item];
  }

  const hasFilter =
    value.keyword !== "" ||
    value.status.length > 0 ||
    value.priority.length > 0 ||
    value.labelId.length > 0 ||
    value.assigneeId !== "" ||
    value.sort !== "";

  return (
    <div className="mb-4 space-y-3 rounded-lg border border-gray-200 bg-white p-4 text-sm">
      <div className="flex flex-wrap items-end gap-3">
        <form
          onSubmit={(e) => e.preventDefault()}
          className="flex-1"
        >
          <label htmlFor="taskKeyword" className="mb-1 block text-xs font-medium text-gray-600">
            キーワード
          </label>
          <input
            id="taskKeyword"
            value={value.keyword}
            onChange={(e) => onChange({ ...value, keyword: e.target.value })}
            placeholder="タイトル・説明を検索"
            className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
        </form>
        <Select
          id="taskAssignee"
          label="担当者"
          value={value.assigneeId}
          onChange={(e) => onChange({ ...value, assigneeId: e.target.value })}
          options={[
            { value: "", label: "すべて" },
            { value: "me", label: "自分" },
            { value: "none", label: "未割り当て" },
            // 本人は「自分」で選べるため、メンバー一覧からは除外して重複を防ぐ
            ...members
              .filter((m) => m.userId !== currentUserId)
              .map((m) => ({ value: String(m.userId), label: m.name })),
          ]}
        />
        <Select
          id="taskSort"
          label="並べ替え"
          value={value.sort}
          onChange={(e) => onChange({ ...value, sort: e.target.value })}
          options={SORT_OPTIONS}
        />
        {hasFilter && (
          <button
            type="button"
            onClick={() => onChange(EMPTY_FILTERS)}
            className="py-1.5 text-xs text-blue-600 hover:underline"
          >
            条件をクリア
          </button>
        )}
      </div>

      {states.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-xs text-gray-500">状態:</span>
          {states.map((s) => (
            <Chip
              key={s.id}
              active={value.status.includes(s.key)}
              onClick={() => onChange({ ...value, status: toggle(value.status, s.key) })}
            >
              {s.name}
            </Chip>
          ))}
        </div>
      )}

      <div className="flex flex-wrap items-center gap-2">
        <span className="text-xs text-gray-500">優先度:</span>
        {PRIORITIES.map((p) => (
          <Chip
            key={p.value}
            active={value.priority.includes(p.value)}
            onClick={() => onChange({ ...value, priority: toggle(value.priority, p.value) })}
          >
            {p.label}
          </Chip>
        ))}
      </div>

      {labels.length > 0 && (
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-xs text-gray-500">ラベル:</span>
          {labels.map((l) => (
            <Chip
              key={l.id}
              active={value.labelId.includes(l.id)}
              onClick={() => onChange({ ...value, labelId: toggle(value.labelId, l.id) })}
            >
              {l.name}
            </Chip>
          ))}
        </div>
      )}
    </div>
  );
}
