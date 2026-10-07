namespace TaskManagementSystem.Api.Dtos.Common;

/// <summary>ページネーション付きの一覧レスポンス(M2 §10.1)。</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
