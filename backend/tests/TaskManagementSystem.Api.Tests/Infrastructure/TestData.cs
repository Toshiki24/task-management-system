using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Tests.Infrastructure;

/// <summary>
/// テストごとに一意なデータを作成するヘルパー。
/// 名前やメールアドレスにGUIDを含め、同じDB内の他テストのデータと取り違えないようにする。
/// </summary>
public static class TestData
{
    public const string DefaultPassword = "Password123!";

    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    public static async Task<User> CreateUserAsync(AppDbContext context, string password = DefaultPassword)
    {
        var name = Unique("ut-user");
        var user = new User
        {
            Name = name,
            Email = $"{name}@example.test",
            // テスト実行時間短縮のためワークファクタを下げる(検証ロジック自体は本番と同じ)
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public static async Task<Project> CreateProjectAsync(
        AppDbContext context, string status = ProjectStatus.Active, long? workspaceId = null)
    {
        // projects.workspace_id は NOT NULL。未指定ならワークスペースを自動作成する
        workspaceId ??= (await CreateWorkspaceAsync(context)).Id;

        var project = new Project
        {
            Name = Unique("ut-project"),
            Description = "単体テスト用プロジェクト",
            Status = status,
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 12, 31),
            WorkspaceId = workspaceId.Value,
        };

        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project;
    }

    public static async Task<Workspace> CreateWorkspaceAsync(AppDbContext context)
    {
        var workspace = new Workspace { Name = Unique("ut-workspace"), Description = "単体テスト用ワークスペース" };
        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync();
        return workspace;
    }

    public static async Task<WorkspaceMember> AddWorkspaceMemberAsync(
        AppDbContext context, long workspaceId, long userId, string role = WorkspaceMemberRole.Member)
    {
        var member = new WorkspaceMember { WorkspaceId = workspaceId, UserId = userId, Role = role };
        context.WorkspaceMembers.Add(member);
        await context.SaveChangesAsync();
        return member;
    }

    /// <summary>プロジェクトと、そのOWNERとして登録済みのユーザーを作成する</summary>
    public static async Task<(Project Project, User Owner)> CreateProjectWithOwnerAsync(
        AppDbContext context, string status = ProjectStatus.Active)
    {
        var project = await CreateProjectAsync(context, status);
        var owner = await CreateUserAsync(context);
        await AddMemberAsync(context, project.Id, owner.Id, ProjectMemberRole.Owner);
        return (project, owner);
    }

    /// <summary>
    /// プロジェクトメンバーを登録する。可視性はワークスペース所属で決まるため、
    /// プロジェクトの所属ワークスペースのメンバーにも登録する(OWNER→WS Admin / MEMBER→WS Member)。
    /// </summary>
    public static async Task<ProjectMember> AddMemberAsync(
        AppDbContext context, long projectId, long userId, string role = ProjectMemberRole.Member)
    {
        var workspaceId = await context.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.WorkspaceId)
            .SingleAsync();

        var alreadyWorkspaceMember = await context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);
        if (!alreadyWorkspaceMember)
        {
            var workspaceRole = role == ProjectMemberRole.Owner
                ? WorkspaceMemberRole.Admin
                : WorkspaceMemberRole.Member;
            await AddWorkspaceMemberAsync(context, workspaceId, userId, workspaceRole);
        }

        var member = new ProjectMember { ProjectId = projectId, UserId = userId, Role = role };
        context.ProjectMembers.Add(member);
        await context.SaveChangesAsync();
        return member;
    }

    public static async Task<TaskItem> CreateTaskAsync(AppDbContext context, long projectId, long? assigneeId = null)
    {
        var task = new TaskItem
        {
            ProjectId = projectId,
            AssigneeId = assigneeId,
            Title = Unique("ut-task"),
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    public static async Task<TaskComment> CreateCommentAsync(AppDbContext context, long taskId, long userId)
    {
        var comment = new TaskComment { TaskId = taskId, UserId = userId, Comment = Unique("ut-comment") };
        context.TaskComments.Add(comment);
        await context.SaveChangesAsync();
        return comment;
    }

    /// <summary>どのテーブルにも存在しないID(bigserialの範囲外)</summary>
    public const long NonExistentId = long.MaxValue;
}
