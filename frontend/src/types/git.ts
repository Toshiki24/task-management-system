export type GitProvider = "GITHUB" | "GITLAB";
export type GitAuthType = "GITHUB_APP" | "OAUTH" | "PAT" | "GROUP_TOKEN";
export type GitConnectionStatus = "ACTIVE" | "DISABLED" | "ERROR";

export interface GitConnection {
  id: number;
  workspaceId: number;
  provider: GitProvider;
  baseUrl: string | null;
  authType: GitAuthType;
  externalAccount: string | null;
  status: GitConnectionStatus;
}

export interface GitConnectionRequestBody {
  provider: GitProvider;
  baseUrl?: string | null;
  authType: GitAuthType;
  secretRef?: string | null;
  externalAccount?: string | null;
}

export interface RepositoryLink {
  id: number;
  projectId: number;
  gitConnectionId: number;
  externalRepoId: string;
  repoFullName: string;
  defaultBranch: string | null;
}

export interface RepositoryLinkRequestBody {
  gitConnectionId: number;
  externalRepoId: string;
  repoFullName: string;
  defaultBranch?: string | null;
}

export interface GitIdentity {
  id: number;
  workspaceId: number;
  userId: number;
  userName: string;
  userEmail: string;
  provider: GitProvider;
  externalUserId: string;
  externalUsername: string | null;
}

export interface GitIdentityRequestBody {
  userId: number;
  provider: GitProvider;
  externalUserId: string;
  externalUsername?: string | null;
}

export type TransitionTrigger = "BRANCH_CREATED" | "PR_OPENED" | "MR_OPENED" | "PR_MERGED" | "MR_MERGED";

export interface TransitionRule {
  id: number;
  trigger: TransitionTrigger;
  toStatusKey: string;
  enabled: boolean;
}

export interface TransitionRuleInput {
  trigger: TransitionTrigger;
  toStatusKey: string;
  enabled: boolean;
}

/** 表示順を兼ねたトリガの一覧とラベル。 */
export const TRANSITION_TRIGGERS: { trigger: TransitionTrigger; label: string }[] = [
  { trigger: "BRANCH_CREATED", label: "ブランチ作成時" },
  { trigger: "PR_OPENED", label: "PR 作成時" },
  { trigger: "PR_MERGED", label: "PR マージ時" },
  { trigger: "MR_OPENED", label: "MR 作成時" },
  { trigger: "MR_MERGED", label: "MR マージ時" },
];

export type GitLinkType = "BRANCH" | "PR" | "MR" | "COMMIT";
export type GitLinkState = "OPEN" | "MERGED" | "CLOSED";

export interface TaskGitLink {
  id: number;
  taskId: number;
  linkType: GitLinkType;
  externalRef: string;
  url: string | null;
  title: string | null;
  state: GitLinkState | null;
}

export const GIT_LINK_TYPE_LABELS: Record<GitLinkType, string> = {
  BRANCH: "ブランチ",
  PR: "プルリクエスト",
  MR: "マージリクエスト",
  COMMIT: "コミット",
};

export const GIT_LINK_STATE_LABELS: Record<GitLinkState, string> = {
  OPEN: "オープン",
  MERGED: "マージ済み",
  CLOSED: "クローズ",
};

export const GIT_PROVIDER_LABELS: Record<GitProvider, string> = {
  GITHUB: "GitHub",
  GITLAB: "GitLab",
};

export const GIT_AUTH_TYPE_LABELS: Record<GitAuthType, string> = {
  GITHUB_APP: "GitHub App",
  OAUTH: "OAuth",
  PAT: "Personal Access Token",
  GROUP_TOKEN: "Project/Group トークン",
};

export const GIT_CONNECTION_STATUS_LABELS: Record<GitConnectionStatus, string> = {
  ACTIVE: "有効",
  DISABLED: "無効",
  ERROR: "エラー",
};
