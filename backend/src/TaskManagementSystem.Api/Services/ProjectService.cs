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

    public async Task<List<ProjectDto>> GetAllAsync()
    {
        var projects = await _dbContext.Projects
            .OrderBy(p => p.Id)
            .ToListAsync();

        return projects.Select(ToDto).ToList();
    }

    public async Task<ProjectDto?> GetByIdAsync(long id)
    {
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

    public async Task<ProjectDto?> UpdateAsync(long id, ProjectRequest request)
    {
        var project = await _dbContext.Projects.FindAsync(id);
        if (project is null)
        {
            return null;
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

        return ToDto(project);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var project = await _dbContext.Projects.FindAsync(id);
        if (project is null)
        {
            return false;
        }

        _dbContext.Projects.Remove(project);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    private static ProjectDto ToDto(Project project) => new(
        project.Id,
        project.Name,
        project.Description,
        project.Status,
        project.StartDate,
        project.EndDate);
}
