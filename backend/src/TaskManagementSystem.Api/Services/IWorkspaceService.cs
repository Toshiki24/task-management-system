using TaskManagementSystem.Api.Dtos.Workspaces;

namespace TaskManagementSystem.Api.Services;

public enum CreateWorkspaceResult
{
    Success,
    Forbidden,
}

public enum UpdateWorkspaceResult
{
    Success,
    NotFound,
    Forbidden,
}

public enum ArchiveWorkspaceResult
{
    Success,
    NotFound,
    Forbidden,
}

public record CreateWorkspaceOutcome(CreateWorkspaceResult Result, WorkspaceDto? Data = null);
public record UpdateWorkspaceOutcome(UpdateWorkspaceResult Result, WorkspaceDto? Data = null);

public interface IWorkspaceService
{
    /// <summary>全ワークスペースの一覧(横断)。System Admin 以外は null(= 403)。</summary>
    Task<List<WorkspaceDto>?> GetAllAsync(long currentUserId);

    /// <summary>自分が所属するワークスペースの一覧(System Admin も自分の所属のみ)。</summary>
    Task<List<WorkspaceDto>> GetMineAsync(long currentUserId);

    /// <summary>ワークスペース詳細。所属しておらず System Admin でもない場合は null(= 404)。</summary>
    Task<WorkspaceDto?> GetByIdAsync(long workspaceId, long currentUserId);

    Task<CreateWorkspaceOutcome> CreateAsync(WorkspaceRequest request, long currentUserId);

    Task<UpdateWorkspaceOutcome> UpdateAsync(long workspaceId, WorkspaceRequest request, long currentUserId);

    Task<ArchiveWorkspaceResult> ArchiveAsync(long workspaceId, long currentUserId);
}
