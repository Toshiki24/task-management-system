using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// 3 スコープ(System / Workspace / Project)の認可判定の結果(Phase 2 M1 基本設計 §3)。
/// </summary>
/// <param name="IsSystemAdmin">インスタンス全体の管理者。全ワークスペースを横断できる。</param>
/// <param name="WorkspaceRole">対象プロジェクトが属するワークスペースでのロール(ADMIN/MEMBER/VIEWER)。所属なしは null。</param>
/// <param name="ProjectRole">対象プロジェクト内のロール(OWNER/MEMBER)。所属なしは null。</param>
public readonly record struct ProjectAccessResult(bool IsSystemAdmin, string? WorkspaceRole, string? ProjectRole)
{
    /// <summary>プロジェクトを閲覧できるか。System Admin か、所属ワークスペースのメンバーなら可視。</summary>
    public bool CanView => IsSystemAdmin || WorkspaceRole is not null;

    /// <summary>ワークスペースの管理(設定・メンバー・招待)が可能か。</summary>
    public bool IsWorkspaceAdmin => IsSystemAdmin || WorkspaceRole == WorkspaceMemberRole.Admin;

    /// <summary>書き込み(作成・編集・削除・コメント)が可能か。Viewer は常に不可。</summary>
    public bool CanWrite =>
        IsSystemAdmin
        || WorkspaceRole == WorkspaceMemberRole.Admin
        || WorkspaceRole == WorkspaceMemberRole.Member;
}

/// <summary>
/// プロジェクトへの所属・権限判定を 1 箇所に集約する(security-review.md 5.1)。
/// 認可の判定を各サービスに個別に書くと判定漏れが起きやすいため、ここに集約する。
/// </summary>
/// <remarks>
/// 存在しないプロジェクト・タスクと、所属していないプロジェクト・タスクはどちらも null(非所属)相当になる。
/// 呼び出し側は両者を区別せず 404 として扱い、他人のプロジェクトの存在自体を開示しない。
/// </remarks>
internal static class ProjectAccess
{
    /// <summary>ユーザーのプロジェクト内権限を返す。所属していない場合は null。</summary>
    public static Task<string?> GetProjectRoleAsync(this AppDbContext dbContext, long projectId, long userId) =>
        dbContext.ProjectMembers
            .Where(pm => pm.ProjectId == projectId && pm.UserId == userId)
            .Select(pm => pm.Role)
            .SingleOrDefaultAsync();

    /// <summary>タスクが属するプロジェクトでの、ユーザーのプロジェクト内権限を返す。所属していない場合は null。</summary>
    public static Task<string?> GetTaskProjectRoleAsync(this AppDbContext dbContext, long taskId, long userId) =>
        dbContext.ProjectMembers
            .Where(pm => pm.UserId == userId && pm.Project.Tasks.Any(t => t.Id == taskId))
            .Select(pm => pm.Role)
            .SingleOrDefaultAsync();

    /// <summary>
    /// プロジェクトに対する 3 スコープの認可判定をまとめて返す(Phase 2 M1)。
    /// プロジェクトが存在しない場合は null(存在を開示しない)。
    /// </summary>
    public static async Task<ProjectAccessResult?> ResolveAccessAsync(
        this AppDbContext dbContext, long projectId, long userId)
    {
        var row = await dbContext.Projects
            .Where(p => p.Id == projectId)
            .Select(p => new
            {
                IsSystemAdmin = dbContext.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.IsSystemAdmin)
                    .FirstOrDefault(),
                // workspace_id は M1 step1 では NULL 許可だが、EF が SQL へ変換するため null 安全(未設定なら所属なし扱い)
                WorkspaceRole = p.Workspace!.Members
                    .Where(m => m.UserId == userId)
                    .Select(m => m.Role)
                    .FirstOrDefault(),
                ProjectRole = p.ProjectMembers
                    .Where(pm => pm.UserId == userId)
                    .Select(pm => pm.Role)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync();

        return row is null
            ? null
            : new ProjectAccessResult(row.IsSystemAdmin, row.WorkspaceRole, row.ProjectRole);
    }

    /// <summary>タスクが属するプロジェクトに対する 3 スコープの認可判定を返す。タスクが存在しない場合は null。</summary>
    public static async Task<ProjectAccessResult?> ResolveTaskAccessAsync(
        this AppDbContext dbContext, long taskId, long userId)
    {
        var row = await dbContext.Tasks
            .Where(t => t.Id == taskId)
            .Select(t => new
            {
                IsSystemAdmin = dbContext.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.IsSystemAdmin)
                    .FirstOrDefault(),
                WorkspaceRole = t.Project.Workspace!.Members
                    .Where(m => m.UserId == userId)
                    .Select(m => m.Role)
                    .FirstOrDefault(),
                ProjectRole = t.Project.ProjectMembers
                    .Where(pm => pm.UserId == userId)
                    .Select(pm => pm.Role)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync();

        return row is null
            ? null
            : new ProjectAccessResult(row.IsSystemAdmin, row.WorkspaceRole, row.ProjectRole);
    }

    /// <summary>
    /// クエリ段階で、ユーザーが閲覧できるプロジェクトだけに絞る(可視性フィルタの土台。Phase 2 M1 §3.4)。
    /// System Admin は全件、それ以外は所属ワークスペースのプロジェクトのみ。アプリ側フィルタに頼らず SQL で除外する。
    /// </summary>
    public static IQueryable<Project> WhereVisibleTo(
        this IQueryable<Project> projects, AppDbContext dbContext, long userId) =>
        projects.Where(p =>
            p.Workspace!.Members.Any(m => m.UserId == userId)
            || dbContext.Users.Any(u => u.Id == userId && u.IsSystemAdmin));

    /// <summary>ユーザーが所属する(=見える)ワークスペースだけに絞る。System Admin は全件。</summary>
    public static IQueryable<Workspace> WhereVisibleTo(
        this IQueryable<Workspace> workspaces, AppDbContext dbContext, long userId) =>
        workspaces.Where(w =>
            w.Members.Any(m => m.UserId == userId)
            || dbContext.Users.Any(u => u.Id == userId && u.IsSystemAdmin));
}
