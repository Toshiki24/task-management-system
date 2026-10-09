using System.Text.Json;
using TaskManagementSystem.Api.Data;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Services;

/// <summary>監査ログの操作種別(Phase 2 M1 §5.2)。</summary>
public static class AuditActions
{
    public const string WorkspaceCreated = "workspace.created";
    public const string WorkspaceUpdated = "workspace.updated";
    public const string WorkspaceArchived = "workspace.archived";
    public const string MemberAdded = "workspace.member.added";
    public const string MemberRoleChanged = "workspace.member.role_changed";
    public const string MemberRemoved = "workspace.member.removed";
    public const string InvitationCreated = "workspace.invitation.created";
    public const string InvitationAccepted = "workspace.invitation.accepted";
    public const string ProjectDeleted = "project.deleted";
    public const string SystemAdminGranted = "user.system_admin.granted";
    public const string SystemAdminRevoked = "user.system_admin.revoked";
    public const string WorkflowStateCreated = "workspace.workflow_state.created";
    public const string WorkflowStateUpdated = "workspace.workflow_state.updated";
    public const string WorkflowStateDeleted = "workspace.workflow_state.deleted";
    public const string LabelCreated = "workspace.label.created";
    public const string LabelUpdated = "workspace.label.updated";
    public const string LabelDeleted = "workspace.label.deleted";
    public const string GitConnectionCreated = "workspace.git_connection.created";
    public const string GitConnectionUpdated = "workspace.git_connection.updated";
    public const string GitConnectionDeleted = "workspace.git_connection.deleted";
    public const string RepositoryLinkCreated = "project.repository_link.created";
    public const string RepositoryLinkDeleted = "project.repository_link.deleted";
    public const string GitIdentityCreated = "workspace.git_identity.created";
    public const string GitIdentityDeleted = "workspace.git_identity.deleted";
}

/// <summary>監査ログの対象種別。</summary>
public static class AuditTargets
{
    public const string Workspace = "workspace";
    public const string Project = "project";
    public const string User = "user";
    public const string Invitation = "invitation";
    public const string WorkflowState = "workflow_state";
    public const string Label = "label";
    public const string GitConnection = "git_connection";
    public const string RepositoryLink = "repository_link";
    public const string GitIdentity = "git_identity";
}

/// <summary>
/// 重要操作の監査ログ記録を 1 箇所に集約する(Phase 2 M1 §5)。
/// 認可ヘルパー(ProjectAccess / WorkspaceAccess)と同じく AppDbContext の拡張として提供し、
/// 各サービスのコンストラクタを変えずに利用できるようにする。
/// </summary>
internal static class Auditing
{
    /// <summary>
    /// 監査ログを 1 件記録する。監査は後追いでよく、記録失敗で本処理を止めないため、例外は飲み込む(§5.2)。
    /// 呼び出し側は本処理の保存に成功したあとに呼ぶこと(監査だけ別 SaveChanges で永続化する)。
    /// </summary>
    public static async Task RecordAuditAsync(
        this AppDbContext dbContext,
        long actorUserId,
        string action,
        string targetType,
        long targetId,
        long? workspaceId = null,
        object? metadata = null)
    {
        try
        {
            dbContext.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorUserId,
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                WorkspaceId = workspaceId,
                // 秘密情報(トークン等)は渡さない。呼び出し側で変更前後などの最小限のみを渡す
                Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata),
            });
            await dbContext.SaveChangesAsync();
        }
        catch
        {
            // 監査ログの記録失敗は本処理の結果に影響させない(ベストエフォート)
        }
    }
}
