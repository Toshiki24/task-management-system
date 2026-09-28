using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Tasks;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

public class TaskService : ITaskService
{
    private readonly AppDbContext _dbContext;

    public TaskService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<TaskDto>?> GetByProjectAsync(long projectId, long currentUserId)
    {
        if (await _dbContext.GetProjectRoleAsync(projectId, currentUserId) is null)
        {
            return null;
        }

        var tasks = await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Id)
            .ToListAsync();

        return tasks.Select(ToDto).ToList();
    }

    public async Task<TaskDto?> GetByIdAsync(long id, long currentUserId)
    {
        if (await _dbContext.GetTaskProjectRoleAsync(id, currentUserId) is null)
        {
            return null;
        }

        var task = await _dbContext.Tasks.FindAsync(id);
        return task is null ? null : ToDto(task);
    }

    public async Task<CreateTaskOutcome> CreateAsync(long projectId, TaskRequest request, long currentUserId)
    {
        if (await _dbContext.GetProjectRoleAsync(projectId, currentUserId) is null)
        {
            return new CreateTaskOutcome(CreateTaskResult.ProjectNotFound);
        }

        switch (await CheckAssigneeAsync(projectId, request.AssigneeId))
        {
            case AssigneeCheck.NotFound:
                return new CreateTaskOutcome(CreateTaskResult.AssigneeNotFound);
            case AssigneeCheck.NotMember:
                return new CreateTaskOutcome(CreateTaskResult.AssigneeNotMember);
        }

        var task = new TaskItem
        {
            ProjectId = projectId,
            AssigneeId = request.AssigneeId,
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
        };

        if (request.Status is not null)
        {
            task.Status = request.Status;
        }

        if (request.Priority is not null)
        {
            task.Priority = request.Priority;
        }

        _dbContext.Tasks.Add(task);
        await _dbContext.SaveChangesAsync();

        return new CreateTaskOutcome(CreateTaskResult.Success, ToDto(task));
    }

    public async Task<UpdateTaskOutcome> UpdateAsync(long id, TaskRequest request, long currentUserId)
    {
        if (await _dbContext.GetTaskProjectRoleAsync(id, currentUserId) is null)
        {
            return new UpdateTaskOutcome(UpdateTaskResult.TaskNotFound);
        }

        var task = await _dbContext.Tasks.FindAsync(id);
        if (task is null)
        {
            return new UpdateTaskOutcome(UpdateTaskResult.TaskNotFound);
        }

        switch (await CheckAssigneeAsync(task.ProjectId, request.AssigneeId))
        {
            case AssigneeCheck.NotFound:
                return new UpdateTaskOutcome(UpdateTaskResult.AssigneeNotFound);
            case AssigneeCheck.NotMember:
                return new UpdateTaskOutcome(UpdateTaskResult.AssigneeNotMember);
        }

        task.AssigneeId = request.AssigneeId;
        task.Title = request.Title;
        task.Description = request.Description;
        task.DueDate = request.DueDate;

        // status/priorityのようなNOT NULL制約付きのenum列は、
        // 未指定(null)の場合に空にできないため既存値を維持する。
        // 一方assigneeId/description/dueDateはNULL許容なので、未指定はnullとして上書きする(PUTの完全上書きセマンティクス)。
        if (request.Status is not null)
        {
            task.Status = request.Status;
        }

        if (request.Priority is not null)
        {
            task.Priority = request.Priority;
        }

        await _dbContext.SaveChangesAsync();

        return new UpdateTaskOutcome(UpdateTaskResult.Success, ToDto(task));
    }

    public async Task<DeleteTaskResult> DeleteAsync(long id, long currentUserId)
    {
        var role = await _dbContext.GetTaskProjectRoleAsync(id, currentUserId);
        if (role is null)
        {
            return DeleteTaskResult.TaskNotFound;
        }

        // タスクに作成者の情報がなく「本人が作成したタスクのみ」を判定できないため、OWNERのみ許可する(security-review.md 5.1)
        if (role != ProjectMemberRole.Owner)
        {
            return DeleteTaskResult.Forbidden;
        }

        var task = await _dbContext.Tasks.FindAsync(id);
        if (task is null)
        {
            return DeleteTaskResult.TaskNotFound;
        }

        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync();

        return DeleteTaskResult.Success;
    }

    private enum AssigneeCheck
    {
        Ok,
        NotFound,
        NotMember,
    }

    /// <summary>担当者はプロジェクトのメンバーに限定する(未割り当ては可)</summary>
    private async Task<AssigneeCheck> CheckAssigneeAsync(long projectId, long? assigneeId)
    {
        if (assigneeId is null)
        {
            return AssigneeCheck.Ok;
        }

        if (!await _dbContext.Users.AnyAsync(u => u.Id == assigneeId))
        {
            return AssigneeCheck.NotFound;
        }

        return await _dbContext.GetProjectRoleAsync(projectId, assigneeId.Value) is null
            ? AssigneeCheck.NotMember
            : AssigneeCheck.Ok;
    }

    private static TaskDto ToDto(TaskItem task) => new(
        task.Id,
        task.ProjectId,
        task.AssigneeId,
        task.Title,
        task.Description,
        task.Status,
        task.Priority,
        task.DueDate);
}
