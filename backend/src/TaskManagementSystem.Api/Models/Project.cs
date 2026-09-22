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
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = ProjectStatus.Active;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ProjectMember> ProjectMembers { get; set; } = new List<ProjectMember>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
