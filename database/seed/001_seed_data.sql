-- ============================================================
-- ローカル開発用シードデータ
--
-- 注意:
--   password_hash はダミー値。
--   実際のログイン確認にはバックエンド側でハッシュを再生成すること。
-- ============================================================

INSERT INTO users (name, email, password_hash) VALUES
    ('管理者ユーザー', 'admin@example.com', '$2a$10$dummyhashadmin000000000000000000000000000000'),
    ('山田太郎', 'yamada@example.com', '$2a$10$dummyhashyamada00000000000000000000000000000'),
    ('鈴木花子', 'suzuki@example.com', '$2a$10$dummyhashsuzuki00000000000000000000000000000');

INSERT INTO projects (name, description, status, start_date, end_date) VALUES
    ('社内システムリニューアル', '既存業務システムの刷新プロジェクト', 'ACTIVE', '2026-09-01', '2026-12-31'),
    ('新規サービス開発', 'MVPリリースに向けた新規開発', 'ACTIVE', '2026-09-15', NULL);

INSERT INTO project_members (project_id, user_id, role) VALUES
    (1, 1, 'OWNER'),
    (1, 2, 'MEMBER'),
    (2, 1, 'OWNER'),
    (2, 3, 'MEMBER');

INSERT INTO tasks (project_id, assignee_id, title, description, status, priority, due_date) VALUES
    (1, 2, '要件ヒアリング', '関係部署へのヒアリング実施', 'DONE', 'HIGH', '2026-09-10'),
    (1, 2, '基本設計書作成', 'システム全体の基本設計をまとめる', 'IN_PROGRESS', 'HIGH', '2026-09-30'),
    (1, NULL, 'DB設計レビュー', 'ER図・DDLのレビュー', 'TODO', 'MEDIUM', '2026-10-05'),
    (2, 3, '画面ワイヤーフレーム作成', '主要画面のワイヤーフレーム作成', 'TODO', 'MEDIUM', '2026-09-25');

INSERT INTO task_comments (task_id, user_id, comment) VALUES
    (1, 1, 'ヒアリング完了しました。議事録を共有します。'),
    (2, 2, '設計書のドラフトを作成中です。'),
    (4, 1, '来週までにレビューお願いします。');
