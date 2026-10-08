using TaskManagementSystem.Api.Dtos.Tasks;

namespace TaskManagementSystem.Api.Services;

public interface IActivityService
{
    /// <summary>タスクのアクティビティを時系列(新しい順)で返す。非所属は null(404 相当)。</summary>
    Task<List<ActivityDto>?> GetByTaskAsync(long taskId, long currentUserId);
}
