using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// プロジェクトへの所属とプロジェクト内権限(OWNER / MEMBER)を判定する。
/// 認可の判定を各サービスに個別に書くと判定漏れが起きやすいため、ここに集約する(security-review.md 5.1)。
/// </summary>
/// <remarks>
/// 存在しないプロジェクト・タスクと、所属していないプロジェクト・タスクはどちらも null(非所属)になる。
/// 呼び出し側は両者を区別せず404として扱い、他人のプロジェクトの存在自体を開示しない。
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
}
