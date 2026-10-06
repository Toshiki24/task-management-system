"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { TaskBoard } from "@/components/task/TaskBoard";
import { TaskForm } from "@/components/task/TaskForm";
import { TaskList } from "@/components/task/TaskList";
import { apiFetch } from "@/lib/api";
import type { Member } from "@/types/member";
import type { Project } from "@/types/project";
import type { Task, TaskRequestBody } from "@/types/task";
import type { WorkflowState } from "@/types/workflow";

const INITIAL_VALUE: TaskRequestBody = {
  title: "",
  description: "",
  assigneeId: null,
  status: "TODO",
  priority: "MEDIUM",
  dueDate: "",
};

export default function ProjectTasksPage() {
  const params = useParams<{ id: string }>();
  const projectId = params.id;

  const [tasks, setTasks] = useState<Task[] | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [states, setStates] = useState<WorkflowState[]>([]);
  const [view, setView] = useState<"list" | "board">("list");
  const [error, setError] = useState<string | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

  useEffect(() => {
    apiFetch<Task[]>(`/projects/${projectId}/tasks`)
      .then(setTasks)
      .catch(() => setError("タスク情報の取得に失敗しました。"));

    apiFetch<Member[]>(`/projects/${projectId}/members`)
      .then(setMembers)
      .catch(() => {
        // 担当者名の表示に使うだけなので、取得できなくても一覧自体は表示する
      });

    // ボード表示の列に使うワークフロー状態を、プロジェクトの所属ワークスペースから取得する
    apiFetch<Project>(`/projects/${projectId}`)
      .then((project) => apiFetch<WorkflowState[]>(`/workspaces/${project.workspaceId}/workflow-states`))
      .then(setStates)
      .catch(() => {
        // 取得できなければボード表示は出さない(リスト表示は影響しない)
      });
  }, [projectId]);

  async function handleCreate(value: TaskRequestBody) {
    await apiFetch<Task>(`/projects/${projectId}/tasks`, {
      method: "POST",
      body: JSON.stringify(value),
    });
    setTasks(await apiFetch<Task[]>(`/projects/${projectId}/tasks`));
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
                className={`rounded-r-md px-3 py-1.5 ${view === "board" ? "bg-blue-600 text-white" : "text-gray-600"}`}
              >
                ボード
              </button>
            </div>
          )}
          <Button type="button" onClick={() => setIsModalOpen(true)}>
            ＋ タスク追加
          </Button>
        </div>
      </div>

      {error && <ErrorMessage message={error} />}
      {!error && tasks === null && <Loading />}
      {tasks && view === "list" && (
        <TaskList
          tasks={tasks}
          members={members}
          onAddClick={() => setIsModalOpen(true)}
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
