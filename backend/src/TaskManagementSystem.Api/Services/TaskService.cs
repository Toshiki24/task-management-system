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

    public async Task<List<TaskDto>?> GetByProjectAsync(long projectId)
    {
        var projectExists = await _dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            return null;
        }

        var tasks = await _dbContext.Tasks
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Id)
            .ToListAsync();

        return tasks.Select(ToDto).ToList();
    }

    public async Task<TaskDto?> GetByIdAsync(long id)
    {
        var task = await _dbContext.Tasks.FindAsync(id);
        return task is null ? null : ToDto(task);
    }

    public async Task<CreateTaskOutcome> CreateAsync(long projectId, TaskRequest request)
    {
        var projectExists = await _dbContext.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            return new CreateTaskOutcome(CreateTaskResult.ProjectNotFound);
        }

        if (request.AssigneeId is not null
            && !await _dbContext.Users.AnyAsync(u => u.Id == request.AssigneeId))
        {
            return new CreateTaskOutcome(CreateTaskResult.AssigneeNotFound);
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

    public async Task<UpdateTaskOutcome> UpdateAsync(long id, TaskRequest request)
    {
        var task = await _dbContext.Tasks.FindAsync(id);
        if (task is null)
        {
            return new UpdateTaskOutcome(UpdateTaskResult.TaskNotFound);
        }

        if (request.AssigneeId is not null
            && !await _dbContext.Users.AnyAsync(u => u.Id == request.AssigneeId))
        {
            return new UpdateTaskOutcome(UpdateTaskResult.AssigneeNotFound);
        }

        task.AssigneeId = request.AssigneeId;
        task.Title = request.Title;
        task.Description = request.Description;
        task.DueDate = request.DueDate;

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

    public async Task<bool> DeleteAsync(long id)
    {
        var task = await _dbContext.Tasks.FindAsync(id);
        if (task is null)
        {
            return false;
        }

        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync();

        return true;
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
