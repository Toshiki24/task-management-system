using TaskManagementSystem.Api.Dtos.Metrics;

namespace TaskManagementSystem.Api.Services;

public interface IMetricsService
{
    /// <summary>プロジェクトの基本指標(進捗・担当別負荷・期限超過)。非所属は null(M5 §2)。</summary>
    Task<MetricsDto?> GetProjectMetricsAsync(long projectId, long currentUserId);
}
