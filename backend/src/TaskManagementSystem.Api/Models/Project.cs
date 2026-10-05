namespace TaskManagementSystem.Api.Models;

public static class ProjectStatus
{
    public const string Active = "ACTIVE";
    public const string Completed = "COMPLETED";
    public const string Archived = "ARCHIVED";
}

public class Project
{
    public long Id { get; set; }

    /// <summary>
    /// 所属ワークスペース(Phase 2 M1。可視性の境界)。
    /// M1 step1 では既存プロジェクト作成を壊さないため NULL 許可で導入し、
    /// プロジェクト作成をワークスペース対応にする step5(API 移設)で NOT NULL 化する。
    /// </summary>
    public long? WorkspaceId { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = ProjectStatus.Active;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Workspace? Workspace { get; set; }
    public ICollection<ProjectMember> ProjectMembers { get; set; } = new List<ProjectMember>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
