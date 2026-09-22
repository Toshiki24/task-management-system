"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { Button } from "@/components/common/Button";
import { ErrorMessage } from "@/components/common/ErrorMessage";
import { Loading } from "@/components/common/Loading";
import { Modal } from "@/components/common/Modal";
import { TaskForm } from "@/components/task/TaskForm";
import { TaskList } from "@/components/task/TaskList";
import { apiFetch } from "@/lib/api";
import type { Member } from "@/types/member";
import type { Task, TaskRequestBody } from "@/types/task";

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
        <Button type="button" onClick={() => setIsModalOpen(true)}>
          ＋ タスク追加
        </Button>
      </div>

      {error && <ErrorMessage message={error} />}
      {!error && tasks === null && <Loading />}
      {tasks && <TaskList tasks={tasks} members={members} />}

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
