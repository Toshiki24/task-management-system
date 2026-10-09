namespace TaskManagementSystem.Api.Services;

public interface ITaskExportService
{
    /// <summary>プロジェクトのタスクを CSV(UTF-8 BOM 付きバイト列)にする。非所属は null(M5 §4)。</summary>
    Task<byte[]?> ExportProjectTasksCsvAsync(long projectId, long currentUserId);
}
