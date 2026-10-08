export interface MentionUser {
  userId: number;
  name: string;
}

export interface Comment {
  id: number;
  taskId: number;
  userId: number;
  userName: string;
  /** 削除済み(isDeleted=true)のときは null。 */
  comment: string | null;
  edited: boolean;
  isDeleted: boolean;
  /** 本文から解決された被メンションユーザー(M3 §3)。 */
  mentions: MentionUser[];
  createdAt: string;
}

export interface CommentRequestBody {
  comment: string;
}
