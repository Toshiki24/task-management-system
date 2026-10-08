namespace TaskManagementSystem.Api.Dtos.Comments;

/// <summary>
/// コメント 1 件(M3 §3)。削除済み(IsDeleted=true)のときは本文を伏せて null を返す。
/// </summary>
public record CommentDto(
    long Id,
    long TaskId,
    long UserId,
    string UserName,
    string? Comment,
    bool Edited,
    bool IsDeleted,
    DateTime CreatedAt
);
