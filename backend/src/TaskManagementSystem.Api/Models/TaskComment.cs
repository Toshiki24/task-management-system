namespace TaskManagementSystem.Api.Models;

public class TaskComment
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public long UserId { get; set; }
    public string Comment { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public TaskItem Task { get; set; } = null!;
    public User User { get; set; } = null!;
}
