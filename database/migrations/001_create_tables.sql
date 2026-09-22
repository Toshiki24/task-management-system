-- ============================================================
-- 案件・タスク管理システム
-- Database Definition Language
--
-- DBMS: PostgreSQL
-- Version: 1.0
--
-- Based on:
--   docs/requirements.md
--   design/basic-design.md
--   docs/database/er-diagram.md
--   docs/database/dll.sql (設計書側マスター)
-- ============================================================

-- ============================================================
-- 1. Users
-- ============================================================

CREATE TABLE users (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

COMMENT ON TABLE users IS 'ユーザー情報';
COMMENT ON COLUMN users.id IS 'ユーザーID';
COMMENT ON COLUMN users.name IS 'ユーザー名';
COMMENT ON COLUMN users.email IS 'メールアドレス';
COMMENT ON COLUMN users.password_hash IS 'ハッシュ化されたパスワード';
COMMENT ON COLUMN users.created_at IS '作成日時';
COMMENT ON COLUMN users.updated_at IS '更新日時';


-- ============================================================
-- 2. Projects
-- ============================================================

CREATE TABLE projects (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    status VARCHAR(30) NOT NULL DEFAULT 'ACTIVE',
    start_date DATE,
    end_date DATE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT chk_projects_status
        CHECK (status IN ('ACTIVE', 'COMPLETED', 'ARCHIVED'))
);

COMMENT ON TABLE projects IS 'プロジェクト情報';
COMMENT ON COLUMN projects.id IS 'プロジェクトID';
COMMENT ON COLUMN projects.name IS 'プロジェクト名';
COMMENT ON COLUMN projects.description IS 'プロジェクト説明';
COMMENT ON COLUMN projects.status IS 'プロジェクト状態';
COMMENT ON COLUMN projects.start_date IS '開始日';
COMMENT ON COLUMN projects.end_date IS '終了日';
COMMENT ON COLUMN projects.created_at IS '作成日時';
COMMENT ON COLUMN projects.updated_at IS '更新日時';


-- ============================================================
-- 3. Project Members
-- ============================================================

CREATE TABLE project_members (
    id BIGSERIAL PRIMARY KEY,
    project_id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    role VARCHAR(30) NOT NULL DEFAULT 'MEMBER',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_project_members_project
        FOREIGN KEY (project_id)
        REFERENCES projects(id)
        ON DELETE CASCADE,

    CONSTRAINT fk_project_members_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE,

    CONSTRAINT uq_project_members_project_user
        UNIQUE (project_id, user_id),

    CONSTRAINT chk_project_members_role
        CHECK (role IN ('OWNER', 'MEMBER'))
);

COMMENT ON TABLE project_members IS 'プロジェクトメンバー';
COMMENT ON COLUMN project_members.id IS 'プロジェクトメンバーID';
COMMENT ON COLUMN project_members.project_id IS 'プロジェクトID';
COMMENT ON COLUMN project_members.user_id IS 'ユーザーID';
COMMENT ON COLUMN project_members.role IS 'プロジェクト内権限';
COMMENT ON COLUMN project_members.created_at IS '登録日時';


-- ============================================================
-- 4. Tasks
-- ============================================================

CREATE TABLE tasks (
    id BIGSERIAL PRIMARY KEY,
    project_id BIGINT NOT NULL,
    assignee_id BIGINT,
    title VARCHAR(200) NOT NULL,
    description TEXT,
    status VARCHAR(30) NOT NULL DEFAULT 'TODO',
    priority VARCHAR(30) NOT NULL DEFAULT 'MEDIUM',
    due_date DATE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_tasks_project
        FOREIGN KEY (project_id)
        REFERENCES projects(id)
        ON DELETE CASCADE,

    CONSTRAINT fk_tasks_assignee
        FOREIGN KEY (assignee_id)
        REFERENCES users(id)
        ON DELETE SET NULL,

    CONSTRAINT chk_tasks_status
        CHECK (status IN ('TODO', 'IN_PROGRESS', 'DONE')),

    CONSTRAINT chk_tasks_priority
        CHECK (priority IN ('LOW', 'MEDIUM', 'HIGH'))
);

COMMENT ON TABLE tasks IS 'タスク情報';
COMMENT ON COLUMN tasks.id IS 'タスクID';
COMMENT ON COLUMN tasks.project_id IS 'プロジェクトID';
COMMENT ON COLUMN tasks.assignee_id IS '担当ユーザーID';
COMMENT ON COLUMN tasks.title IS 'タスク名';
COMMENT ON COLUMN tasks.description IS 'タスク説明';
COMMENT ON COLUMN tasks.status IS 'タスク状態';
COMMENT ON COLUMN tasks.priority IS '優先度';
COMMENT ON COLUMN tasks.due_date IS '期限';
COMMENT ON COLUMN tasks.created_at IS '作成日時';
COMMENT ON COLUMN tasks.updated_at IS '更新日時';


-- ============================================================
-- 5. Task Comments
-- ============================================================

CREATE TABLE task_comments (
    id BIGSERIAL PRIMARY KEY,
    task_id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    comment TEXT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_task_comments_task
        FOREIGN KEY (task_id)
        REFERENCES tasks(id)
        ON DELETE CASCADE,

    CONSTRAINT fk_task_comments_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
);

COMMENT ON TABLE task_comments IS 'タスクコメント';
COMMENT ON COLUMN task_comments.id IS 'コメントID';
COMMENT ON COLUMN task_comments.task_id IS 'タスクID';
COMMENT ON COLUMN task_comments.user_id IS 'コメント投稿者ID';
COMMENT ON COLUMN task_comments.comment IS 'コメント本文';
COMMENT ON COLUMN task_comments.created_at IS '投稿日時';


-- ============================================================
-- 6. Task Status Histories
-- ============================================================
-- Phase 2機能。
-- MVPでは必須機能として使用しない。
-- ============================================================

CREATE TABLE task_status_histories (
    id BIGSERIAL PRIMARY KEY,
    task_id BIGINT NOT NULL,
    changed_by BIGINT NOT NULL,
    from_status VARCHAR(30),
    to_status VARCHAR(30) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_task_status_histories_task
        FOREIGN KEY (task_id)
        REFERENCES tasks(id)
        ON DELETE CASCADE,

    CONSTRAINT fk_task_status_histories_user
        FOREIGN KEY (changed_by)
        REFERENCES users(id)
        ON DELETE CASCADE,

    CONSTRAINT chk_task_status_histories_from_status
        CHECK (
            from_status IS NULL
            OR from_status IN ('TODO', 'IN_PROGRESS', 'DONE')
        ),

    CONSTRAINT chk_task_status_histories_to_status
        CHECK (
            to_status IN ('TODO', 'IN_PROGRESS', 'DONE')
        )
);

COMMENT ON TABLE task_status_histories IS 'タスクステータス変更履歴';
COMMENT ON COLUMN task_status_histories.id IS '履歴ID';
COMMENT ON COLUMN task_status_histories.task_id IS 'タスクID';
COMMENT ON COLUMN task_status_histories.changed_by IS '変更ユーザーID';
COMMENT ON COLUMN task_status_histories.from_status IS '変更前ステータス';
COMMENT ON COLUMN task_status_histories.to_status IS '変更後ステータス';
COMMENT ON COLUMN task_status_histories.created_at IS '変更日時';


-- ============================================================
-- 7. Indexes
-- ============================================================

-- Users
-- email UNIQUE制約によるインデックスが自動作成されるため、
-- email用の追加インデックスは作成しない。


-- Project Members
CREATE INDEX idx_project_members_project_id
    ON project_members(project_id);

CREATE INDEX idx_project_members_user_id
    ON project_members(user_id);


-- Tasks
CREATE INDEX idx_tasks_project_id
    ON tasks(project_id);

CREATE INDEX idx_tasks_assignee_id
    ON tasks(assignee_id);

CREATE INDEX idx_tasks_status
    ON tasks(status);

CREATE INDEX idx_tasks_due_date
    ON tasks(due_date);


-- Task Comments
CREATE INDEX idx_task_comments_task_id
    ON task_comments(task_id);


-- Task Status Histories
CREATE INDEX idx_task_status_histories_task_id
    ON task_status_histories(task_id);
