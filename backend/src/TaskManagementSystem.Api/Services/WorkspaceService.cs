using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Workspaces;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

public class WorkspaceService : IWorkspaceService
{
    private readonly AppDbContext _dbContext;

    public WorkspaceService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<WorkspaceDto>?> GetAllAsync(long currentUserId)
    {
        // 全ワークスペースの横断一覧は System Admin のみ(所属外の存在を一般ユーザーに開示しない)
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return null;
        }

        return await _dbContext.Workspaces
            .OrderBy(w => w.Id)
            .Select(w => new WorkspaceDto(
                w.Id,
                w.Name,
                w.Description,
                w.ArchivedAt != null,
                w.Members.Where(m => m.UserId == currentUserId).Select(m => m.Role).FirstOrDefault(),
                w.CreatedAt))
            .ToListAsync();
    }

    public async Task<List<WorkspaceDto>> GetMineAsync(long currentUserId)
    {
        // System Admin であっても「自分の所属」は所属分のみ(横断は GetAllAsync)
        return await _dbContext.Workspaces
            .Where(w => w.Members.Any(m => m.UserId == currentUserId))
            .OrderBy(w => w.Id)
            .Select(w => new WorkspaceDto(
                w.Id,
                w.Name,
                w.Description,
                w.ArchivedAt != null,
                w.Members.Where(m => m.UserId == currentUserId).Select(m => m.Role).FirstOrDefault(),
                w.CreatedAt))
            .ToListAsync();
    }

    public async Task<WorkspaceDto?> GetByIdAsync(long workspaceId, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            // 存在しない or 所属外(非 System Admin)は区別せず 404(存在を開示しない)
            return null;
        }

        return await _dbContext.Workspaces
            .Where(w => w.Id == workspaceId)
            .Select(w => new WorkspaceDto(
                w.Id,
                w.Name,
                w.Description,
                w.ArchivedAt != null,
                w.Members.Where(m => m.UserId == currentUserId).Select(m => m.Role).FirstOrDefault(),
                w.CreatedAt))
            .SingleAsync();
    }

    public async Task<CreateWorkspaceOutcome> CreateAsync(WorkspaceRequest request, long currentUserId)
    {
        // ワークスペース作成は System Admin のみ(M1 §3.3)
        if (!await _dbContext.IsSystemAdminAsync(currentUserId))
        {
            return new CreateWorkspaceOutcome(CreateWorkspaceResult.Forbidden);
        }

        var workspace = new Workspace
        {
            Name = request.Name!,
            Description = request.Description,
        };

        _dbContext.Workspaces.Add(workspace);
        await _dbContext.SaveChangesAsync();

        return new CreateWorkspaceOutcome(
            CreateWorkspaceResult.Success,
            ToDto(workspace, myRole: null));
    }

    public async Task<UpdateWorkspaceOutcome> UpdateAsync(
        long workspaceId, WorkspaceRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new UpdateWorkspaceOutcome(UpdateWorkspaceResult.NotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return new UpdateWorkspaceOutcome(UpdateWorkspaceResult.Forbidden);
        }

        var workspace = await _dbContext.Workspaces.SingleAsync(w => w.Id == workspaceId);
        workspace.Name = request.Name!;
        workspace.Description = request.Description;
        await _dbContext.SaveChangesAsync();

        return new UpdateWorkspaceOutcome(
            UpdateWorkspaceResult.Success,
            ToDto(workspace, access.Value.Role));
    }

    public async Task<ArchiveWorkspaceResult> ArchiveAsync(long workspaceId, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return ArchiveWorkspaceResult.NotFound;
        }

        if (!access.Value.IsAdmin)
        {
            return ArchiveWorkspaceResult.Forbidden;
        }

        var workspace = await _dbContext.Workspaces.SingleAsync(w => w.Id == workspaceId);
        // 冪等: 既にアーカイブ済みなら時刻は変えない
        workspace.ArchivedAt ??= DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _dbContext.SaveChangesAsync();

        return ArchiveWorkspaceResult.Success;
    }

    // 作成・更新直後のレスポンス用(メモリ上のエンティティから組み立てる)
    private static WorkspaceDto ToDto(Workspace workspace, string? myRole) => new(
        workspace.Id,
        workspace.Name,
        workspace.Description,
        workspace.ArchivedAt is not null,
        myRole,
        workspace.CreatedAt);
}
