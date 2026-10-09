"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { apiFetch, formatApiErrorMessage } from "@/lib/api";
import {
  TRANSITION_TRIGGERS,
  type TransitionRule,
  type TransitionRuleInput,
  type TransitionTrigger,
} from "@/types/git";
import type { WorkflowState } from "@/types/workflow";

interface TransitionRuleEditorProps {
  /** ワークフロー状態の取得元ワークスペース。 */
  workspaceId: number;
  /** 指定するとプロジェクト個別ルール(WS 既定の上書き)を編集する。未指定は WS 既定。 */
  projectId?: number;
  /** 呼び出しユーザーがルールを編集できるか(WS Admin / Project OWNER)。 */
  canManage: boolean;
}

/** トリガごとの設定(遷移先キーと有効フラグ)。"" は「遷移しない」。 */
type RuleState = Record<TransitionTrigger, { toStatusKey: string; enabled: boolean }>;

const EMPTY: RuleState = {
  BRANCH_CREATED: { toStatusKey: "", enabled: false },
  PR_OPENED: { toStatusKey: "", enabled: false },
  PR_MERGED: { toStatusKey: "", enabled: false },
  MR_OPENED: { toStatusKey: "", enabled: false },
  MR_MERGED: { toStatusKey: "", enabled: false },
};

export function TransitionRuleEditor({ workspaceId, projectId, canManage }: TransitionRuleEditorProps) {
  const [states, setStates] = useState<WorkflowState[]>([]);
  const [rules, setRules] = useState<RuleState>(EMPTY);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [loaded, setLoaded] = useState(false);

  // プロジェクト指定時はプロジェクト個別ルール、未指定は WS 既定を対象にする
  const rulesPath = projectId ? `/projects/${projectId}/transition-rules` : `/workspaces/${workspaceId}/transition-rules`;

  useEffect(() => {
    Promise.all([
      apiFetch<WorkflowState[]>(`/workspaces/${workspaceId}/workflow-states`),
      apiFetch<TransitionRule[]>(rulesPath),
    ])
      .then(([wsStates, wsRules]) => {
        setStates(wsStates);
        const next = structuredClone(EMPTY);
        for (const r of wsRules) {
          next[r.trigger] = { toStatusKey: r.toStatusKey, enabled: r.enabled };
        }
        setRules(next);
        setLoaded(true);
      })
      .catch(() => setError("自動遷移ルールの取得に失敗しました。"));
  }, [workspaceId, rulesPath]);

  function update(trigger: TransitionTrigger, patch: Partial<{ toStatusKey: string; enabled: boolean }>) {
    setSaved(false);
    setRules((prev) => ({ ...prev, [trigger]: { ...prev[trigger], ...patch } }));
  }

  async function handleSave() {
    setError(null);
    setIsSaving(true);
    try {
      // 遷移先が選ばれているトリガだけを送る
      const payload: TransitionRuleInput[] = TRANSITION_TRIGGERS
        .filter(({ trigger }) => rules[trigger].toStatusKey !== "")
        .map(({ trigger }) => ({
          trigger,
          toStatusKey: rules[trigger].toStatusKey,
          enabled: rules[trigger].enabled,
        }));
      await apiFetch(rulesPath, {
        method: "PUT",
        body: JSON.stringify({ rules: payload }),
      });
      setSaved(true);
    } catch (err) {
      setError(formatApiErrorMessage(err, "自動遷移ルールの保存に失敗しました。"));
    } finally {
      setIsSaving(false);
    }
  }

  if (!canManage) {
    return <p className="text-sm text-gray-500">自動遷移ルールの編集はワークスペース管理者(ADMIN)のみ可能です。</p>;
  }

  if (!loaded && !error) {
    return <p className="text-sm text-gray-500">読み込み中...</p>;
  }

  return (
    <div>
      {error && <ErrorMessage message={error} />}

      <ul className="mb-4 divide-y divide-gray-100 rounded-md border border-gray-200">
        {TRANSITION_TRIGGERS.map(({ trigger, label }) => (
          <li key={trigger} className="flex flex-wrap items-center gap-3 px-3 py-2 text-sm">
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={rules[trigger].enabled}
                onChange={(e) => update(trigger, { enabled: e.target.checked })}
                className="h-4 w-4"
              />
              <span className="w-28 text-gray-900">{label}</span>
            </label>
            <span className="text-gray-400">→</span>
            <select
              aria-label={`${label} の遷移先`}
              value={rules[trigger].toStatusKey}
              onChange={(e) => update(trigger, { toStatusKey: e.target.value })}
              className="rounded-md border border-gray-300 px-2 py-1 text-sm text-gray-900"
            >
              <option value="">遷移しない</option>
              {states.map((s) => (
                <option key={s.key} value={s.key}>
                  {s.name}
                </option>
              ))}
            </select>
          </li>
        ))}
      </ul>

      <div className="flex items-center gap-3">
        <Button type="button" variant="primary" onClick={handleSave} disabled={isSaving}>
          {isSaving ? "更新中..." : "遷移ルールを更新"}
        </Button>
        {saved && <span className="text-sm text-green-600">保存しました。</span>}
      </div>
    </div>
  );
}
