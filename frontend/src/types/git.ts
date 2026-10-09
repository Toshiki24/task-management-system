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
