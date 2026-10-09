using TaskManagementSystem.Api.Dtos.Git;

namespace TaskManagementSystem.Api.Services;

public enum TransitionRuleResult
{
    Success,
    NotFound,
    Forbidden,
    InvalidStatus,
}

public record TransitionRuleOutcome(TransitionRuleResult Result, List<TransitionRuleDto>? Data = null);

public interface ITransitionRuleService
{
    Task<List<TransitionRuleDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId);
    Task<TransitionRuleOutcome> ReplaceWorkspaceAsync(long workspaceId, PutTransitionRulesRequest request, long currentUserId);

    Task<List<TransitionRuleDto>?> GetByProjectAsync(long projectId, long currentUserId);
    Task<TransitionRuleOutcome> ReplaceProjectAsync(long projectId, PutTransitionRulesRequest request, long currentUserId);

    /// <summary>
    /// トリガに対する遷移先の状態キーを解決する(M4 §8)。プロジェクト個別ルール(enabled)を優先し、
    /// 無ければワークスペース既定(enabled)を使う。該当が無ければ null。
    /// </summary>
    Task<string?> ResolveToStatusAsync(long projectId, long workspaceId, string trigger);
}
