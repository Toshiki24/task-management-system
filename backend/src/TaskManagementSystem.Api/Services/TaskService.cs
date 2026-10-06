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
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
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
        var access = await _dbContext.ResolveTaskAccessAsync(id, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        var task = await _dbContext.Tasks.FindAsync(id);
        return task is null ? null : ToDto(task);
    }

    public async Task<CreateTaskOutcome> CreateAsync(long projectId, TaskRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(projectId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new CreateTaskOutcome(CreateTaskResult.ProjectNotFound);
        }

        // Viewer はタスクを作成できない
        if (!access.Value.CanWrite)
        {
            return new CreateTaskOutcome(CreateTaskResult.Forbidden);
        }

        switch (await CheckAssigneeAsync(projectId, request.AssigneeId))
        {
            case AssigneeCheck.NotFound:
                return new CreateTaskOutcome(CreateTaskResult.AssigneeNotFound);
            case AssigneeCheck.NotMember:
                return new CreateTaskOutcome(CreateTaskResult.AssigneeNotMember);
        }

        // status はワークスペースのワークフロー(workflow_states)に対して検証する(M2 §3.2)。
        // 未指定なら既定状態(is_default)にする。固定の TODO/IN_PROGRESS/DONE には縛らない。
        var workspaceId = await _dbContext.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.WorkspaceId)
            .FirstAsync();
        var (validKeys, defaultKey) = await GetWorkflowAsync(workspaceId);

        if (request.Status is not null && !validKeys.Contains(request.Status))
        {
            return new CreateTaskOutcome(CreateTaskResult.InvalidStatus);
        }

        var task = new TaskItem
        {
            ProjectId = projectId,
            AssigneeId = request.AssigneeId,
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
            Status = request.Status ?? defaultKey ?? TaskItemStatus.Todo,
        };

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
        var access = await _dbContext.ResolveTaskAccessAsync(id, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new UpdateTaskOutcome(UpdateTaskResult.TaskNotFound);
        }

        // Viewer はタスクを編集できない
        if (!access.Value.CanWrite)
        {
            return new UpdateTaskOutcome(UpdateTaskResult.Forbidden);
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

        // status が指定された場合は、タスクの属するワークスペースのワークフローに対して検証する(M2 §3.2)
        if (request.Status is not null)
        {
            var workspaceId = await _dbContext.Tasks
                .Where(t => t.Id == id)
                .Select(t => t.Project.WorkspaceId)
                .FirstAsync();
            var (validKeys, _) = await GetWorkflowAsync(workspaceId);
            if (!validKeys.Contains(request.Status))
            {
                return new UpdateTaskOutcome(UpdateTaskResult.InvalidStatus);
            }
        }

        task.AssigneeId = request.AssigneeId;
        task.Title = request.Title;
        task.Description = request.Description;
        task.DueDate = request.DueDate;

        // status/priorityのようなNOT NULL制約付きの列は、
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
        var access = await _dbContext.ResolveTaskAccessAsync(id, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return DeleteTaskResult.TaskNotFound;
        }

        // タスクの削除は WS Admin / Member が可能(Viewer は不可。M1 §3.3)
        if (!access.Value.CanWrite)
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

    /// <summary>
    /// ワークスペースのワークフロー状態キー一覧と既定キーを返す(M2 §3.2)。
    /// 状態が未設定の WS でも落ちないよう、既定が無ければ先頭、それも無ければ null を返す。
    /// </summary>
    private async Task<(List<string> ValidKeys, string? DefaultKey)> GetWorkflowAsync(long workspaceId)
    {
        var states = await _dbContext.WorkflowStates
            .Where(s => s.WorkspaceId == workspaceId)
            .OrderBy(s => s.Position)
            .ThenBy(s => s.Id)
            .Select(s => new { s.Key, s.IsDefault })
            .ToListAsync();

        var validKeys = states.Select(s => s.Key).ToList();
        var defaultKey = states.FirstOrDefault(s => s.IsDefault)?.Key ?? validKeys.FirstOrDefault();
        return (validKeys, defaultKey);
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
