using TaskManagementSystem.Api.Dtos.Metrics;

namespace TaskManagementSystem.Api.Services;

public enum TaskImportResult
{
    Success,
    ProjectNotFound,
    Forbidden,
    TooLarge,
}

public record TaskImportOutcome(TaskImportResult Result, ImportResultDto? Data = null);

public interface ITaskImportService
{
    /// <summary>CSV からタスクを一括作成する(M5 §4)。CanWrite。行単位で検証しエラーを返す。</summary>
    Task<TaskImportOutcome> ImportProjectTasksAsync(long projectId, string csv, long currentUserId);
}
