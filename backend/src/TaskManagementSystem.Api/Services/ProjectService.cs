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

    public async Task<List<ProjectDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            // 所属していないワークスペース(または存在しない)は 404 相当(存在を開示しない)
            return null;
        }

        // 可視性はワークスペース所属で決まる。所属していれば WS 内の全プロジェクトが見える
        var projects = await _dbContext.Projects
            .Where(p => p.WorkspaceId == workspaceId)
            .OrderBy(p => p.Id)
            .ToListAsync();

        return projects.Select(ToDto).ToList();
    }

    public async Task<ProjectDto?> GetByIdAsync(long id, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(id, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        var project = await _dbContext.Projects.FindAsync(id);
        return project is null ? null : ToDto(project);
    }

    public async Task<CreateProjectOutcome> CreateAsync(long workspaceId, ProjectRequest request, long creatorUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, creatorUserId);
        if (access is null || !access.Value.CanView)
        {
            return new CreateProjectOutcome(CreateProjectResult.WorkspaceNotFound);
        }

        // Viewer はプロジェクトを作成できない(WS Admin / Member のみ)
        if (!access.Value.CanWrite)
        {
            return new CreateProjectOutcome(CreateProjectResult.Forbidden);
        }

        var project = new Project
        {
            WorkspaceId = workspaceId,
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

        return new CreateProjectOutcome(CreateProjectResult.Success, ToDto(project));
    }

    public async Task<UpdateProjectOutcome> UpdateAsync(long id, ProjectRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(id, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new UpdateProjectOutcome(UpdateProjectResult.ProjectNotFound);
        }

        // プロジェクトの編集はプロジェクト OWNER または WS Admin(または System Admin)
        if (!access.Value.CanManageProject)
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

        // status は NOT NULL 制約付きのため、未指定(null)の場合は既存値を維持する。
        // 一方 description/日付列は NULL 許容なので、未指定は null として上書きする(PUT の完全上書きセマンティクス)。
        if (request.Status is not null)
        {
            project.Status = request.Status;
        }

        await _dbContext.SaveChangesAsync();

        return new UpdateProjectOutcome(UpdateProjectResult.Success, ToDto(project));
    }

    public async Task<DeleteProjectResult> DeleteAsync(long id, long currentUserId)
    {
        var access = await _dbContext.ResolveAccessAsync(id, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return DeleteProjectResult.ProjectNotFound;
        }

        if (!access.Value.CanManageProject)
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
