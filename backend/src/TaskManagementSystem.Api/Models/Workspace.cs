namespace TaskManagementSystem.Api.Models;

/// <summary>
/// ワークスペース(チーム/部署)。可視性と権限の境界になる(Phase 2 ADR 0001)。
/// 1 インスタンス = 1 組織とし、組織内をワークスペースで分ける。
/// </summary>
public class Workspace
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>NULL=有効、値あり=アーカイブ済み。</summary>
    public DateTime? ArchivedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
    public ICollection<WorkflowState> WorkflowStates { get; set; } = new List<WorkflowState>();
}
