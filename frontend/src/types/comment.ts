export interface Comment {
  id: number;
  taskId: number;
  userId: number;
  userName: string;
  /** 削除済み(isDeleted=true)のときは null。 */
  comment: string | null;
  edited: boolean;
  isDeleted: boolean;
  createdAt: string;
}

export interface CommentRequestBody {
  comment: string;
}
