namespace TaskManagementSystem.Api.Dtos.Workspaces;

/// <summary>
/// ワークスペース情報。<paramref name="MyRole"/> は呼び出しユーザーのそのワークスペースでのロール
/// (所属していない System Admin では null)。
/// </summary>
public record WorkspaceDto(
    long Id,
    string Name,
    string? Description,
    bool IsArchived,
    string? MyRole,
    DateTime CreatedAt
);
