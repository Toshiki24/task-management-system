export interface Comment {
  id: number;
  taskId: number;
  userId: number;
  userName: string;
  comment: string;
  createdAt: string;
}

export interface CommentRequestBody {
  comment: string;
}
