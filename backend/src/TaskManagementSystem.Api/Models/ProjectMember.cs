namespace TaskManagementSystem.Api.Models;

public static class ProjectMemberRole
{
    public const string Owner = "OWNER";
    public const string Member = "MEMBER";
}

public class ProjectMember
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public long UserId { get; set; }
    public string Role { get; set; } = ProjectMemberRole.Member;
    public DateTime CreatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public User User { get; set; } = null!;
}
