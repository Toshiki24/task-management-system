using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// ワークスペースに対する認可判定の結果(Phase 2 M1 §3)。
/// </summary>
/// <param name="IsSystemAdmin">インスタンス全体の管理者。全ワークスペースを横断できる。</param>
/// <param name="Role">対象ワークスペースでのロール(ADMIN/MEMBER/VIEWER)。所属なしは null。</param>
public readonly record struct WorkspaceAccessResult(bool IsSystemAdmin, string? Role)
{
    /// <summary>ワークスペースを閲覧できるか。所属していない非 System Admin には存在も開示しない。</summary>
    public bool CanView => IsSystemAdmin || Role is not null;

    /// <summary>ワークスペースの管理(設定・メンバー・招待)が可能か。</summary>
    public bool IsAdmin => IsSystemAdmin || Role == WorkspaceMemberRole.Admin;

    /// <summary>書き込み(プロジェクト作成等)が可能か。Viewer は常に不可。</summary>
    public bool CanWrite =>
        IsSystemAdmin
        || Role == WorkspaceMemberRole.Admin
        || Role == WorkspaceMemberRole.Member;
}

/// <summary>
/// ワークスペースへの所属・権限判定を 1 箇所に集約する(Phase 2 M1 §3.4)。
/// </summary>
/// <remarks>
/// 存在しないワークスペースと、所属していないワークスペースはどちらも「見えない」扱いにできるよう、
/// 呼び出し側は <see cref="WorkspaceAccessResult.CanView"/> が false なら 404 を返し、存在自体を開示しない。
/// </remarks>
internal static class WorkspaceAccess
{
    /// <summary>
    /// ワークスペースに対する認可判定をまとめて返す。ワークスペースが存在しない場合は null。
    /// </summary>
    public static async Task<WorkspaceAccessResult?> ResolveWorkspaceAccessAsync(
        this AppDbContext dbContext, long workspaceId, long userId)
    {
        var row = await dbContext.Workspaces
            .Where(w => w.Id == workspaceId)
            .Select(w => new
            {
                IsSystemAdmin = dbContext.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.IsSystemAdmin)
                    .FirstOrDefault(),
                Role = w.Members
                    .Where(m => m.UserId == userId)
                    .Select(m => m.Role)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync();

        return row is null
            ? null
            : new WorkspaceAccessResult(row.IsSystemAdmin, row.Role);
    }

    /// <summary>ユーザーが System Admin かどうか。</summary>
    public static Task<bool> IsSystemAdminAsync(this AppDbContext dbContext, long userId) =>
        dbContext.Users.AnyAsync(u => u.Id == userId && u.IsSystemAdmin);
}
