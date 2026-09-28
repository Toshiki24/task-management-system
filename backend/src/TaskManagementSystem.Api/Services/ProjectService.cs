using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Projects;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

public class ProjectService : IProjectService
{
    private readonly AppDbContext _dbContext;

    public ProjectService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ProjectDto>> GetAllAsync(long currentUserId)
    {
        // 自分が所属しているプロジェクトのみ返す
        var projects = await _dbContext.Projects
            .Where(p => p.ProjectMembers.Any(pm => pm.UserId == currentUserId))
            .OrderBy(p => p.Id)
            .ToListAsync();

        return projects.Select(ToDto).ToList();
    }

    public async Task<ProjectDto?> GetByIdAsync(long id, long currentUserId)
    {
        if (await _dbContext.GetProjectRoleAsync(id, currentUserId) is null)
        {
            return null;
        }

        var project = await _dbContext.Projects.FindAsync(id);
        return project is null ? null : ToDto(project);
    }

    public async Task<ProjectDto> CreateAsync(ProjectRequest request, long creatorUserId)
    {
        var project = new Project
        {
            Name = request.Name,
            Description = request.Description,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
        };

        if (request.Status is not null)
        {
            project.Status = request.Status;
        }

        // プロジェクト作成とOWNERとしてのメンバー登録は、
        // 一方だけ成功する状態を防ぐため同一トランザクションで行う(基本設計書§30)
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        _dbContext.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = creatorUserId,
            Role = ProjectMemberRole.Owner,
        });
        await _dbContext.SaveChangesAsync();

        await transaction.CommitAsync();

        return ToDto(project);
    }

    public async Task<UpdateProjectOutcome> UpdateAsync(long id, ProjectRequest request, long currentUserId)
    {
        var role = await _dbContext.GetProjectRoleAsync(id, currentUserId);
        if (role is null)
        {
            return new UpdateProjectOutcome(UpdateProjectResult.ProjectNotFound);
        }

        if (role != ProjectMemberRole.Owner)
        {
            return new UpdateProjectOutcome(UpdateProjectResult.Forbidden);
        }

        var project = await _dbContext.Projects.FindAsync(id);
        if (project is null)
        {
            return new UpdateProjectOutcome(UpdateProjectResult.ProjectNotFound);
        }

        project.Name = request.Name;
        project.Description = request.Description;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;

        // status/priorityのようなNOT NULL制約付きのenum列は、
        // 未指定(null)の場合に空にできないため既存値を維持する。
        // 一方description/日付列はNULL許容なので、未指定はnullとして上書きする(PUTの完全上書きセマンティクス)。
        if (request.Status is not null)
        {
            project.Status = request.Status;
        }

        await _dbContext.SaveChangesAsync();

        return new UpdateProjectOutcome(UpdateProjectResult.Success, ToDto(project));
    }

    public async Task<DeleteProjectResult> DeleteAsync(long id, long currentUserId)
    {
        var role = await _dbContext.GetProjectRoleAsync(id, currentUserId);
        if (role is null)
        {
            return DeleteProjectResult.ProjectNotFound;
        }

        if (role != ProjectMemberRole.Owner)
        {
            return DeleteProjectResult.Forbidden;
        }

        var project = await _dbContext.Projects.FindAsync(id);
        if (project is null)
        {
            return DeleteProjectResult.ProjectNotFound;
        }

        _dbContext.Projects.Remove(project);
        await _dbContext.SaveChangesAsync();

        return DeleteProjectResult.Success;
    }

    private static ProjectDto ToDto(Project project) => new(
        project.Id,
        project.Name,
        project.Description,
        project.Status,
        project.StartDate,
        project.EndDate);
}
