"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { BulkActionBar } from "@/components/task/BulkActionBar";
import { CyclePlanning } from "@/components/task/CyclePlanning";
import { MilestonePlanning } from "@/components/task/MilestonePlanning";
import { SavedViewBar } from "@/components/task/SavedViewBar";
import { TaskBoard } from "@/components/task/TaskBoard";
import { TaskFilterBar, EMPTY_FILTERS, buildTaskQuery, type TaskFilters } from "@/components/task/TaskFilterBar";
import { TaskForm } from "@/components/task/TaskForm";
import { TaskList } from "@/components/task/TaskList";
import { apiFetch } from "@/lib/api";
import { fetchCurrentUser } from "@/lib/auth";
import { useLiveRefresh } from "@/lib/useLiveRefresh";
import { statusLabelMap } from "@/lib/taskLabels";
import type { Label } from "@/types/label";
import type { Member } from "@/types/member";
import type { Project } from "@/types/project";
import type { SavedView } from "@/types/savedView";
import type { BulkUpdateBody, Task, TaskRequestBody } from "@/types/task";
import type { WorkflowState } from "@/types/workflow";

const INITIAL_VALUE: TaskRequestBody = {
  title: "",
  description: "",
  assigneeId: null,
  status: "TODO",
  priority: "MEDIUM",
  dueDate: "",
  estimatePoints: null,
  labelIds: [],
};

export default function ProjectTasksPage() {
  const params = useParams<{ id: string }>();
  const projectId = params.id;

  const [tasks, setTasks] = useState<Task[] | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [states, setStates] = useState<WorkflowState[]>([]);
  const [labels, setLabels] = useState<Label[]>([]);
  const [views, setViews] = useState<SavedView[]>([]);
  const [workspaceId, setWorkspaceId] = useState<number | null>(null);
  const [currentUserId, setCurrentUserId] = useState<number | null>(null);
  const [filters, setFilters] = useState<TaskFilters>(EMPTY_FILTERS);
  const [view, setView] = useState<"list" | "board" | "cycle" | "milestone">("list");
  const [error, setError] = useState<string | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [bulkMode, setBulkMode] = useState(false);
  const [selectedIds, setSelectedIds] = useState<number[]>([]);

  // フィルタ条件が変わるたびに、条件付きでタスクを取り直す
  useEffect(() => {
    apiFetch<Task[]>(`/projects/${projectId}/tasks${buildTaskQuery(filters)}`)
      .then(setTasks)
      .catch(() => setError("タスク情報の取得に失敗しました。"));
    // 条件が変わると対象が変わるため選択はリセットする
    setSelectedIds([]);
  }, [projectId, filters]);

  function toggleSelect(taskId: number) {
    setSelectedIds((ids) => (ids.includes(taskId) ? ids.filter((id) => id !== taskId) : [...ids, taskId]));
  }

  function toggleSelectAll(taskIds: number[]) {
    setSelectedIds((ids) => (taskIds.every((id) => ids.includes(id)) ? [] : taskIds));
  }

  async function handleBulkApply(body: BulkUpdateBody) {
    await apiFetch(`/projects/${projectId}/tasks/bulk`, {
      method: "PATCH",
      body: JSON.stringify(body),
    });
    await reloadTasks();
    setSelectedIds([]);
  }

  useEffect(() => {
    apiFetch<Member[]>(`/projects/${projectId}/members`)
      .then(setMembers)
      .catch(() => {
        // 担当者名の表示に使うだけなので、取得できなくても一覧自体は表示する
      });

    // 担当者フィルタで本人を「自分」と二重に出さないため、現在のユーザーIDを取得する
    fetchCurrentUser()
      .then((user) => setCurrentUserId(user?.id ?? null))
      .catch(() => setCurrentUserId(null));

    // ボードの列・フィルタの候補に使うワークフロー状態とラベルを、所属ワークスペースから取得する
    apiFetch<Project>(`/projects/${projectId}`)
      .then(async (project) => {
        setWorkspaceId(project.workspaceId);
        const [wsStates, wsLabels, wsViews] = await Promise.all([
          apiFetch<WorkflowState[]>(`/workspaces/${project.workspaceId}/workflow-states`),
          apiFetch<Label[]>(`/workspaces/${project.workspaceId}/labels`),
          apiFetch<SavedView[]>(`/workspaces/${project.workspaceId}/views`),
        ]);
        setStates(wsStates);
        setLabels(wsLabels);
        setViews(wsViews);
      })
      .catch(() => {
        // 取得できなければボード/フィルタ/保存ビューの候補は出さない(リスト表示は影響しない)
      });
  }, [projectId]);

  async function reloadTasks() {
    setTasks(await apiFetch<Task[]>(`/projects/${projectId}/tasks${buildTaskQuery(filters)}`));
  }

  // 他者の変更を反映する軽量リアルタイム更新(M3 §7)。
  // タスク作成モーダル表示中は、入力の裏で一覧が変わらないよう停止する。
  useLiveRefresh(reloadTasks, { enabled: !isModalOpen });

  async function handleCreate(value: TaskRequestBody) {
    await apiFetch<Task>(`/projects/${projectId}/tasks`, {
      method: "POST",
      body: JSON.stringify(value),
    });
    await reloadTasks();
    setIsModalOpen(false);
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-lg font-bold text-gray-900">タスク一覧</h1>
        <div className="flex items-center gap-3">
          {states.length > 0 && (
            <div className="flex rounded-md border border-gray-300 text-sm" role="tablist" aria-label="表示切替">
              <button
                type="button"
                role="tab"
                aria-selected={view === "list"}
                onClick={() => setView("list")}
                className={`rounded-l-md px-3 py-1.5 ${view === "list" ? "bg-blue-600 text-white" : "text-gray-600"}`}
              >
                リスト
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={view === "board"}
                onClick={() => setView("board")}
                className={`px-3 py-1.5 ${view === "board" ? "bg-blue-600 text-white" : "text-gray-600"}`}
              >
                ボード
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={view === "cycle"}
                onClick={() => setView("cycle")}
                className={`px-3 py-1.5 ${view === "cycle" ? "bg-blue-600 text-white" : "text-gray-600"}`}
              >
                サイクル
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={view === "milestone"}
                onClick={() => setView("milestone")}
                className={`rounded-r-md px-3 py-1.5 ${view === "milestone" ? "bg-blue-600 text-white" : "text-gray-600"}`}
              >
                マイルストーン
              </button>
            </div>
          )}
          {view === "list" && (
            <Button
              type="button"
              variant="secondary"
              aria-pressed={bulkMode}
              onClick={() => {
                setBulkMode((on) => !on);
                setSelectedIds([]);
              }}
            >
              {bulkMode ? "一括操作を終了" : "一括操作"}
            </Button>
          )}
          <Button type="button" onClick={() => setIsModalOpen(true)}>
            ＋ タスク追加
          </Button>
        </div>
      </div>

      {view === "list" && (
        <>
          <SavedViewBar
            workspaceId={workspaceId}
            views={views}
            filters={filters}
            viewType={view}
            onApply={(nextFilters, nextView) => {
              setFilters(nextFilters);
              setView(nextView);
            }}
            onViewsChanged={setViews}
          />
          <TaskFilterBar
            states={states}
            labels={labels}
            members={members}
            value={filters}
            onChange={setFilters}
            currentUserId={currentUserId}
          />
        </>
      )}

      {error && <ErrorMessage message={error} />}
      {!error && tasks === null && <Loading />}
      {tasks && view === "list" && bulkMode && selectedIds.length > 0 && (
        <BulkActionBar
          selectedIds={selectedIds}
          states={states}
          members={members}
          labels={labels}
          onApply={handleBulkApply}
          onClear={() => setSelectedIds([])}
        />
      )}
      {tasks && view === "list" && (
        <TaskList
          tasks={tasks}
          members={members}
          statusLabels={statusLabelMap(states)}
          onAddClick={() => setIsModalOpen(true)}
          selectable={bulkMode}
          selectedIds={selectedIds}
          onToggleSelect={toggleSelect}
          onToggleSelectAll={toggleSelectAll}
        />
      )}
      {tasks && view === "board" && (
        <TaskBoard
          projectId={projectId}
          tasks={tasks}
          states={states}
          members={members}
          onChanged={setTasks}
        />
      )}
      {tasks && view === "cycle" && (
        <CyclePlanning
          projectId={projectId}
          tasks={tasks}
          statusLabels={statusLabelMap(states)}
          onReloadTasks={reloadTasks}
        />
      )}
      {tasks && view === "milestone" && (
        <MilestonePlanning
          projectId={projectId}
          tasks={tasks}
          statusLabels={statusLabelMap(states)}
          onReloadTasks={reloadTasks}
        />
      )}

      <Modal
        isOpen={isModalOpen}
        title="タスク追加"
        onClose={() => setIsModalOpen(false)}
      >
        <TaskForm
          projectId={projectId}
          initialValue={INITIAL_VALUE}
          submitLabel="登録"
          submittingLabel="登録中..."
          onSubmit={handleCreate}
          onCancel={() => setIsModalOpen(false)}
        />
      </Modal>
    </div>
  );
}
