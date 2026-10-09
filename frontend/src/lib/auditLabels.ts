/** 監査ログのアクション・対象コードを日本語表示にする(M5 §5)。 */

const ACTION_LABELS: Record<string, string> = {
  "workspace.created": "ワークスペース作成",
  "workspace.updated": "ワークスペース更新",
  "workspace.archived": "ワークスペースのアーカイブ",
  "workspace.member.added": "メンバー追加",
  "workspace.member.role_changed": "メンバーのロール変更",
  "workspace.member.removed": "メンバー削除",
  "workspace.invitation.created": "招待の作成",
  "workspace.invitation.accepted": "招待の受諾",
  "project.deleted": "プロジェクト削除",
  "user.system_admin.granted": "System Admin 付与",
  "user.system_admin.revoked": "System Admin 剥奪",
  "workspace.workflow_state.created": "ワークフロー状態の作成",
  "workspace.workflow_state.updated": "ワークフロー状態の更新",
  "workspace.workflow_state.deleted": "ワークフロー状態の削除",
  "workspace.label.created": "ラベル作成",
  "workspace.label.updated": "ラベル更新",
  "workspace.label.deleted": "ラベル削除",
  "workspace.git_connection.created": "Git 連携の作成",
  "workspace.git_connection.updated": "Git 連携の更新",
  "workspace.git_connection.deleted": "Git 連携の削除",
  "project.repository_link.created": "リポジトリ連携の作成",
  "project.repository_link.deleted": "リポジトリ連携の削除",
  "workspace.git_identity.created": "Git アイデンティティの作成",
  "workspace.git_identity.deleted": "Git アイデンティティの削除",
};

const TARGET_LABELS: Record<string, string> = {
  workspace: "ワークスペース",
  project: "プロジェクト",
  user: "ユーザー",
  invitation: "招待",
  workflow_state: "ワークフロー状態",
  label: "ラベル",
  git_connection: "Git 連携",
  repository_link: "リポジトリ連携",
  git_identity: "Git アイデンティティ",
};

export function auditActionLabel(action: string): string {
  return ACTION_LABELS[action] ?? action;
}

export function auditTargetLabel(targetType: string): string {
  return TARGET_LABELS[targetType] ?? targetType;
}

/** フィルタ用：主要なアクションの選択肢(値→表示)。 */
export const AUDIT_ACTION_OPTIONS: { value: string; label: string }[] = [
  { value: "", label: "すべてのアクション" },
  ...Object.entries(ACTION_LABELS).map(([value, label]) => ({ value, label })),
];
