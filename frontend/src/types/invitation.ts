import type { WorkspaceRole } from "@/types/workspace";

export interface InvitationPreview {
  workspaceId: number;
  workspaceName: string;
  email: string;
  role: WorkspaceRole;
  isExistingUser: boolean;
}

export interface CreateInvitationBody {
  email: string;
  role: WorkspaceRole;
}

export interface AcceptInvitationBody {
  name?: string;
  password?: string;
}

export interface AcceptInvitationResult {
  workspaceId: number;
  userId: number;
  accountCreated: boolean;
}
