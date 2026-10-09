using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Dtos.Git;
using TaskManagementSystem.Api.Models;
using TaskManagementSystem.Api.Services.Git;

namespace TaskManagementSystem.Api.Services;

/// <summary>
/// ワークスペース単位の Git 接続の管理(M4 §4)。参照/設定は WS Admin。資格情報そのものは扱わず、
/// Secrets Manager 等への参照(secret_ref)のみを保持する。すべて LINQ/EF。
/// </summary>
public class GitConnectionService : IGitConnectionService
{
    private readonly AppDbContext _dbContext;
    private readonly IGitProviderResolver _providers;

    public GitConnectionService(AppDbContext dbContext, IGitProviderResolver providers)
    {
        _dbContext = dbContext;
        _providers = providers;
    }

    public async Task<List<GitConnectionDto>?> GetByWorkspaceAsync(long workspaceId, long currentUserId)
    {
        // 一覧はワークスペースのメンバーなら参照できる(リポジトリ連携で接続を選ぶため)。
        // 資格情報(secret_ref)は DTO に含めないため露出しない。作成・更新・削除は WS Admin 限定。
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return null;
        }

        return await _dbContext.GitConnections
            .Where(c => c.WorkspaceId == workspaceId)
            .OrderBy(c => c.Id)
            .Select(c => new GitConnectionDto(
                c.Id, c.WorkspaceId, c.Provider, c.BaseUrl, c.AuthType, c.ExternalAccount, c.Status))
            .ToListAsync();
    }

    public async Task<GitConnectionOutcome> CreateAsync(
        long workspaceId, CreateGitConnectionRequest request, long currentUserId)
    {
        var access = await _dbContext.ResolveWorkspaceAccessAsync(workspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new GitConnectionOutcome(GitConnectionResult.WorkspaceNotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return new GitConnectionOutcome(GitConnectionResult.Forbidden);
        }

        // 同一 WS・プロバイダ・アカウントの重複接続を抑止する
        var duplicate = await _dbContext.GitConnections.AnyAsync(c =>
            c.WorkspaceId == workspaceId
            && c.Provider == request.Provider
            && c.ExternalAccount == request.ExternalAccount);
        if (duplicate)
        {
            return new GitConnectionOutcome(GitConnectionResult.Duplicate);
        }

        var connection = new GitConnection
        {
            WorkspaceId = workspaceId,
            Provider = request.Provider,
            BaseUrl = Normalize(request.BaseUrl),
            AuthType = request.AuthType,
            SecretRef = Normalize(request.SecretRef),
            ExternalAccount = Normalize(request.ExternalAccount),
            Status = GitConnectionStatus.Active,
        };
        _dbContext.GitConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.GitConnectionCreated, AuditTargets.GitConnection, connection.Id, workspaceId,
            new { connection.Provider, connection.AuthType });

        return new GitConnectionOutcome(GitConnectionResult.Success, ToDto(connection));
    }

    public async Task<GitConnectionOutcome> UpdateAsync(
        long connectionId, UpdateGitConnectionRequest request, long currentUserId)
    {
        var connection = await _dbContext.GitConnections.FirstOrDefaultAsync(c => c.Id == connectionId);
        if (connection is null)
        {
            return new GitConnectionOutcome(GitConnectionResult.ConnectionNotFound);
        }

        var access = await _dbContext.ResolveWorkspaceAccessAsync(connection.WorkspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return new GitConnectionOutcome(GitConnectionResult.ConnectionNotFound);
        }

        if (!access.Value.IsAdmin)
        {
            return new GitConnectionOutcome(GitConnectionResult.Forbidden);
        }

        connection.BaseUrl = Normalize(request.BaseUrl);
        connection.AuthType = request.AuthType;
        // secret_ref は指定があれば更新、空なら従来値を維持(再入力なしで他項目を変えられる)
        if (!string.IsNullOrWhiteSpace(request.SecretRef))
        {
            connection.SecretRef = request.SecretRef.Trim();
        }
        connection.ExternalAccount = Normalize(request.ExternalAccount);
        connection.Status = request.Status;
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.GitConnectionUpdated, AuditTargets.GitConnection, connection.Id,
            connection.WorkspaceId, new { connection.AuthType, connection.Status });

        return new GitConnectionOutcome(GitConnectionResult.Success, ToDto(connection));
    }

    public async Task<GitConnectionResult> DeleteAsync(long connectionId, long currentUserId)
    {
        var connection = await _dbContext.GitConnections.FirstOrDefaultAsync(c => c.Id == connectionId);
        if (connection is null)
        {
            return GitConnectionResult.ConnectionNotFound;
        }

        var access = await _dbContext.ResolveWorkspaceAccessAsync(connection.WorkspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return GitConnectionResult.ConnectionNotFound;
        }

        if (!access.Value.IsAdmin)
        {
            return GitConnectionResult.Forbidden;
        }

        // 連携リポジトリ(repository_links)は ON DELETE CASCADE で連動削除される
        _dbContext.GitConnections.Remove(connection);
        await _dbContext.SaveChangesAsync();

        await _dbContext.RecordAuditAsync(
            currentUserId, AuditActions.GitConnectionDeleted, AuditTargets.GitConnection, connection.Id,
            connection.WorkspaceId, new { connection.Provider });

        return GitConnectionResult.Success;
    }

    public async Task<GitConnectionResult> TestAsync(long connectionId, long currentUserId)
    {
        var connection = await _dbContext.GitConnections.FirstOrDefaultAsync(c => c.Id == connectionId);
        if (connection is null)
        {
            return GitConnectionResult.ConnectionNotFound;
        }

        var access = await _dbContext.ResolveWorkspaceAccessAsync(connection.WorkspaceId, currentUserId);
        if (access is null || !access.Value.CanView)
        {
            return GitConnectionResult.ConnectionNotFound;
        }

        if (!access.Value.IsAdmin)
        {
            return GitConnectionResult.Forbidden;
        }

        var provider = _providers.Resolve(connection.Provider);
        if (provider is null)
        {
            return GitConnectionResult.ProviderUnavailable;
        }

        // 疎通確認: リポジトリ一覧の取得を試み、成否で接続状態を更新する(M4 §4)
        try
        {
            await provider.ListRepositoriesAsync(
                new GitConnectionRef(connection.Id, connection.BaseUrl, connection.SecretRef));
            connection.Status = GitConnectionStatus.Active;
            await _dbContext.SaveChangesAsync();
            return GitConnectionResult.Success;
        }
        catch
        {
            connection.Status = GitConnectionStatus.Error;
            await _dbContext.SaveChangesAsync();
            return GitConnectionResult.TestFailed;
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static GitConnectionDto ToDto(GitConnection c) =>
        new(c.Id, c.WorkspaceId, c.Provider, c.BaseUrl, c.AuthType, c.ExternalAccount, c.Status);
}
