namespace TaskManagementSystem.Api.Dtos.Comments;

/// <summary>@メンションで参照されたユーザー(M3 §3)。</summary>
public record MentionUserDto(long UserId, string Name);

/// <summary>
/// コメント 1 件(M3 §3)。削除済み(IsDeleted=true)のときは本文を伏せて null を返す。
/// Mentions は本文から解決された被メンションユーザー(削除済みは空)。
/// </summary>
public record CommentDto(
    long Id,
    long TaskId,
    long UserId,
    string UserName,
    string? Comment,
    bool Edited,
    bool IsDeleted,
    IReadOnlyList<MentionUserDto> Mentions,
    DateTime CreatedAt
);
