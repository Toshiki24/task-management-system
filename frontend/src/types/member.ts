export type ProjectMemberRole = "OWNER" | "MEMBER";

export interface Member {
  userId: number;
  name: string;
  email: string;
  role: ProjectMemberRole;
}

export interface AddMemberRequestBody {
  userId: number;
  role: ProjectMemberRole;
}
