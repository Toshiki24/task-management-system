using TaskManagementSystem.Api.Dtos.Workflow;

namespace TaskManagementSystem.Api.Services;

public enum CreateWorkflowStateResult
{
    Success,
    WorkspaceNotFound,
    Forbidden,
    DuplicateKey,
}

public enum UpdateWorkflowStateResult
{
    Success,
    NotFound,
    Forbidden,
}

public enum DeleteWorkflowStateResult
{
    Success,
    NotFound,
    Forbidden,
    /// <summary>既定状態(is_default)は削除できない。</summary>
    DefaultState,
    /// <summary>使用中(タスクが存在)なのに付け替え先(moveTo)が指定/解決されていない。</summary>
    InUse,
}

public record CreateWorkflowStateOutcome(CreateWorkflowStateResult Result, WorkflowStateDto? Data = null);

public record UpdateWorkflowStateOutcome(UpdateWorkflowStateResult Result, WorkflowStateDto? Data = null);

public interface IWorkflowStateService
{
    Task<List<WorkflowStateDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId);

    Task<CreateWorkflowStateOutcome> CreateAsync(
        long workspaceId, WorkflowStateCreateRequest request, long currentUserId);

    Task<UpdateWorkflowStateOutcome> UpdateAsync(
        long workspaceId, long stateId, WorkflowStateUpdateRequest request, long currentUserId);

    Task<DeleteWorkflowStateResult> DeleteAsync(
        long workspaceId, long stateId, string? moveToKey, long currentUserId);
}
