using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<WorkflowState> WorkflowStates => Set<WorkflowState>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<TaskLabel> TaskLabels => Set<TaskLabel>();
    public DbSet<TaskChecklistItem> TaskChecklistItems => Set<TaskChecklistItem>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<SavedView> SavedViews => Set<SavedView>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskStatusHistory> TaskStatusHistories => Set<TaskStatusHistory>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ============================================================
        // Users
        // ============================================================
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.IsSystemAdmin).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // email UNIQUE制約によるインデックスのみ使用し、追加インデックスは作成しない
            entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("users_email_key");
        });

        // ============================================================
        // Projects
        // ============================================================
        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired().HasDefaultValue(ProjectStatus.Active);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // 所属ワークスペース(可視性の境界)。既定WSへバックフィル後に NOT NULL 化する(マイグレーション参照)
            entity.HasIndex(e => e.WorkspaceId).HasDatabaseName("idx_projects_workspace_id");
            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Projects)
                .HasForeignKey(e => e.WorkspaceId)
                .HasConstraintName("fk_projects_workspace")
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(tb => tb.HasCheckConstraint(
                "chk_projects_status",
                "status IN ('ACTIVE', 'COMPLETED', 'ARCHIVED')"));
        });

        // ============================================================
        // ProjectMembers
        // ============================================================
        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.Property(e => e.Role).HasMaxLength(30).IsRequired().HasDefaultValue(ProjectMemberRole.Member);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => new { e.ProjectId, e.UserId })
                .IsUnique()
                .HasDatabaseName("uq_project_members_project_user");

            entity.HasIndex(e => e.ProjectId).HasDatabaseName("idx_project_members_project_id");
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_project_members_user_id");

            entity.HasOne(e => e.Project)
                .WithMany(p => p.ProjectMembers)
                .HasForeignKey(e => e.ProjectId)
                .HasConstraintName("fk_project_members_project")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.ProjectMembers)
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("fk_project_members_user")
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(tb => tb.HasCheckConstraint(
                "chk_project_members_role",
                "role IN ('OWNER', 'MEMBER')"));
        });

        // ============================================================
        // Tasks
        // ============================================================
        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.ToTable("tasks");

            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            // status はワークフロー状態キー(workflow_states.key, 最大50)を指すため 50 文字に合わせる
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired().HasDefaultValue(TaskItemStatus.Todo);
            entity.Property(e => e.Priority).HasMaxLength(30).IsRequired().HasDefaultValue(TaskItemPriority.Medium);
            entity.Property(e => e.BoardPosition).HasDefaultValue(0d);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.ProjectId).HasDatabaseName("idx_tasks_project_id");
            entity.HasIndex(e => e.AssigneeId).HasDatabaseName("idx_tasks_assignee_id");
            entity.HasIndex(e => e.ParentTaskId).HasDatabaseName("idx_tasks_parent_task_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_tasks_status");
            entity.HasIndex(e => new { e.ProjectId, e.Status, e.BoardPosition })
                .HasDatabaseName("idx_tasks_board_order");
            entity.HasIndex(e => e.DueDate).HasDatabaseName("idx_tasks_due_date");

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(e => e.ProjectId)
                .HasConstraintName("fk_tasks_project")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Assignee)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(e => e.AssigneeId)
                .HasConstraintName("fk_tasks_assignee")
                .OnDelete(DeleteBehavior.SetNull);

            // サブタスク(自己参照)。親を削除したら子も削除する(M2 §7.1)
            entity.HasOne(e => e.ParentTask)
                .WithMany(e => e.Subtasks)
                .HasForeignKey(e => e.ParentTaskId)
                .HasConstraintName("fk_tasks_parent_task")
                .OnDelete(DeleteBehavior.Cascade);

            // status はワークスペースごとのワークフロー(workflow_states.key)を指す動的な値のため、
            // 固定値の CHECK 制約は設けない。妥当性はサービス層で LINQ により WS の状態集合に対して検証する(M2 §3.2)。
            entity.ToTable(tb =>
            {
                tb.HasCheckConstraint("chk_tasks_priority", "priority IN ('LOW', 'MEDIUM', 'HIGH')");
            });
        });

        // ============================================================
        // TaskChecklistItems (Phase 2 M2)
        // ============================================================
        modelBuilder.Entity<TaskChecklistItem>(entity =>
        {
            entity.ToTable("task_checklist_items");

            entity.Property(e => e.Content).HasMaxLength(500).IsRequired();
            entity.Property(e => e.IsDone).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.TaskId).HasDatabaseName("idx_task_checklist_items_task_id");

            entity.HasOne(e => e.Task)
                .WithMany(t => t.ChecklistItems)
                .HasForeignKey(e => e.TaskId)
                .HasConstraintName("fk_task_checklist_items_task")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // TaskDependencies (Phase 2 M2 §7.3 ブロック/被ブロック)
        // ============================================================
        modelBuilder.Entity<TaskDependency>(entity =>
        {
            entity.ToTable("task_dependencies");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // 同一の有向辺(blocking→blocked)は 1 本まで
            entity.HasIndex(e => new { e.BlockingTaskId, e.BlockedTaskId })
                .IsUnique()
                .HasDatabaseName("uq_task_dependencies_pair");
            entity.HasIndex(e => e.BlockingTaskId).HasDatabaseName("idx_task_dependencies_blocking_task_id");
            entity.HasIndex(e => e.BlockedTaskId).HasDatabaseName("idx_task_dependencies_blocked_task_id");

            // どちらのタスクが消えても依存辺は消す(逆参照ナビは張らない)
            entity.HasOne(e => e.BlockingTask)
                .WithMany()
                .HasForeignKey(e => e.BlockingTaskId)
                .HasConstraintName("fk_task_dependencies_blocking_task")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.BlockedTask)
                .WithMany()
                .HasForeignKey(e => e.BlockedTaskId)
                .HasConstraintName("fk_task_dependencies_blocked_task")
                .OnDelete(DeleteBehavior.Cascade);

            // 自己依存は DB でも拒否する(循環のうち最小のもの)
            entity.ToTable(tb => tb.HasCheckConstraint(
                "chk_task_dependencies_no_self",
                "blocking_task_id <> blocked_task_id"));
        });

        // ============================================================
        // Activities (Phase 2 M3 §5 アクティビティ)
        // ============================================================
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.ToTable("activities");

            entity.Property(e => e.Verb).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Payload).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => new { e.ProjectId, e.CreatedAt }).HasDatabaseName("idx_activities_project_created");
            entity.HasIndex(e => new { e.TaskId, e.CreatedAt }).HasDatabaseName("idx_activities_task_created");

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .HasConstraintName("fk_activities_project")
                .OnDelete(DeleteBehavior.Cascade);

            // タスクが消えればそのタスクの活動も消す(プロジェクト直下の活動は TaskId=NULL)
            entity.HasOne(e => e.Task)
                .WithMany()
                .HasForeignKey(e => e.TaskId)
                .HasConstraintName("fk_activities_task")
                .OnDelete(DeleteBehavior.Cascade);

            // 実行者が消えても活動履歴は残す(監査性。audit_logs と同方針)
            entity.HasOne(e => e.ActorUser)
                .WithMany()
                .HasForeignKey(e => e.ActorUserId)
                .HasConstraintName("fk_activities_actor_user")
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(tb => tb.HasCheckConstraint(
                "chk_activities_verb",
                "verb IN ('CREATED', 'UPDATED', 'MOVED', 'COMMENTED')"));
        });

        // ============================================================
        // TaskComments
        // ============================================================
        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.Property(e => e.Comment).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.TaskId).HasDatabaseName("idx_task_comments_task_id");

            entity.HasOne(e => e.Task)
                .WithMany(t => t.Comments)
                .HasForeignKey(e => e.TaskId)
                .HasConstraintName("fk_task_comments_task")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.TaskComments)
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("fk_task_comments_user")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // TaskStatusHistories (Phase 2)
        // ============================================================
        modelBuilder.Entity<TaskStatusHistory>(entity =>
        {
            // from_status/to_status はワークフロー状態キー(最大50)を記録する。
            // 状態は WS ごとに可変のため、固定値の CHECK 制約は設けない(tasks.status と同方針。M2 §4.2)。
            entity.Property(e => e.FromStatus).HasMaxLength(50);
            entity.Property(e => e.ToStatus).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.TaskId).HasDatabaseName("idx_task_status_histories_task_id");

            entity.HasOne(e => e.Task)
                .WithMany(t => t.StatusHistories)
                .HasForeignKey(e => e.TaskId)
                .HasConstraintName("fk_task_status_histories_task")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ChangedByUser)
                .WithMany(u => u.TaskStatusHistories)
                .HasForeignKey(e => e.ChangedBy)
                .HasConstraintName("fk_task_status_histories_user")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // RefreshTokens (security-review.md 5.3)
        // ============================================================
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            // SHA-256のハッシュ値(16進数64文字)
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.RevokedReason).HasMaxLength(30);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("refresh_tokens_token_hash_key");
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_refresh_tokens_user_id");

            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("fk_refresh_tokens_user")
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(tb =>
            {
                tb.HasCheckConstraint(
                    "chk_refresh_tokens_revoked_reason",
                    "revoked_reason IS NULL OR revoked_reason IN ('ROTATED', 'LOGOUT', 'REUSE_DETECTED')");
            });
        });

        // ============================================================
        // Workspaces (Phase 2 M1)
        // ============================================================
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // ============================================================
        // WorkflowStates (Phase 2 M2)
        // ============================================================
        modelBuilder.Entity<WorkflowState>(entity =>
        {
            entity.ToTable("workflow_states");

            entity.Property(e => e.Key).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // 状態キーは WS 内で一意(タスクの status はこの key を指す)
            entity.HasIndex(e => new { e.WorkspaceId, e.Key })
                .IsUnique()
                .HasDatabaseName("uq_workflow_states_workspace_key");
            entity.HasIndex(e => e.WorkspaceId).HasDatabaseName("idx_workflow_states_workspace_id");

            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.WorkflowStates)
                .HasForeignKey(e => e.WorkspaceId)
                .HasConstraintName("fk_workflow_states_workspace")
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(tb => tb.HasCheckConstraint(
                "chk_workflow_states_category",
                "category IN ('BACKLOG', 'TODO', 'IN_PROGRESS', 'DONE', 'CANCELLED')"));
        });

        // ============================================================
        // Labels / TaskLabels (Phase 2 M2)
        // ============================================================
        modelBuilder.Entity<Label>(entity =>
        {
            entity.ToTable("labels");

            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => new { e.WorkspaceId, e.Name })
                .IsUnique()
                .HasDatabaseName("uq_labels_workspace_name");
            entity.HasIndex(e => e.WorkspaceId).HasDatabaseName("idx_labels_workspace_id");

            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Labels)
                .HasForeignKey(e => e.WorkspaceId)
                .HasConstraintName("fk_labels_workspace")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskLabel>(entity =>
        {
            entity.ToTable("task_labels");

            entity.HasKey(e => new { e.TaskId, e.LabelId });
            entity.HasIndex(e => e.LabelId).HasDatabaseName("idx_task_labels_label_id");

            entity.HasOne(e => e.Task)
                .WithMany(t => t.TaskLabels)
                .HasForeignKey(e => e.TaskId)
                .HasConstraintName("fk_task_labels_task")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Label)
                .WithMany(l => l.TaskLabels)
                .HasForeignKey(e => e.LabelId)
                .HasConstraintName("fk_task_labels_label")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================
        // SavedViews (Phase 2 M2)
        // ============================================================
        modelBuilder.Entity<SavedView>(entity =>
        {
            entity.ToTable("saved_views");

            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ViewType).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Filters).HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.WorkspaceId).HasDatabaseName("idx_saved_views_workspace_id");
            entity.HasIndex(e => e.OwnerUserId).HasDatabaseName("idx_saved_views_owner_user_id");

            entity.HasOne(e => e.Workspace)
                .WithMany()
                .HasForeignKey(e => e.WorkspaceId)
                .HasConstraintName("fk_saved_views_workspace")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.OwnerUser)
                .WithMany()
                .HasForeignKey(e => e.OwnerUserId)
                .HasConstraintName("fk_saved_views_owner_user")
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(tb => tb.HasCheckConstraint(
                "chk_saved_views_view_type",
                "view_type IN ('LIST', 'BOARD')"));
        });

        // ============================================================
        // WorkspaceMembers (Phase 2 M1)
        // ============================================================
        modelBuilder.Entity<WorkspaceMember>(entity =>
        {
            entity.Property(e => e.Role).HasMaxLength(20).IsRequired().HasDefaultValue(WorkspaceMemberRole.Member);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // 複数ワークスペース所属は許す(user_id 単独は一意にしない)が、同一WSへの二重所属は禁止する
            entity.HasIndex(e => new { e.WorkspaceId, e.UserId })
                .IsUnique()
                .HasDatabaseName("uq_workspace_members_workspace_user");
            entity.HasIndex(e => e.WorkspaceId).HasDatabaseName("idx_workspace_members_workspace_id");
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_workspace_members_user_id");

            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Members)
                .HasForeignKey(e => e.WorkspaceId)
                .HasConstraintName("fk_workspace_members_workspace")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.WorkspaceMemberships)
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("fk_workspace_members_user")
                .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(tb => tb.HasCheckConstraint(
                "chk_workspace_members_role",
                "role IN ('ADMIN', 'MEMBER', 'VIEWER')"));
        });

        // ============================================================
        // Invitations (Phase 2 M1)
        // ============================================================
        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(20).IsRequired().HasDefaultValue(WorkspaceMemberRole.Member);
            // SHA-256 のハッシュ値(16進数64文字)。平文トークンは保存しない
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("invitations_token_hash_key");
            entity.HasIndex(e => new { e.WorkspaceId, e.Email }).HasDatabaseName("idx_invitations_workspace_email");

            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Invitations)
                .HasForeignKey(e => e.WorkspaceId)
                .HasConstraintName("fk_invitations_workspace")
                .OnDelete(DeleteBehavior.Cascade);

            // 招待者が削除されても招待履歴は残す(SetNull はできないため Restrict)
            entity.HasOne(e => e.InvitedByUser)
                .WithMany()
                .HasForeignKey(e => e.InvitedBy)
                .HasConstraintName("fk_invitations_invited_by")
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(tb => tb.HasCheckConstraint(
                "chk_invitations_role",
                "role IN ('ADMIN', 'MEMBER', 'VIEWER')"));
        });

        // ============================================================
        // AuditLogs (Phase 2 M1)
        // ============================================================
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
            entity.Property(e => e.TargetType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Metadata).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("idx_audit_logs_created_at");
            entity.HasIndex(e => e.WorkspaceId).HasDatabaseName("idx_audit_logs_workspace_id");
            entity.HasIndex(e => e.ActorUserId).HasDatabaseName("idx_audit_logs_actor_user_id");

            // 監査ログは実行者が削除されても残す
            entity.HasOne(e => e.ActorUser)
                .WithMany()
                .HasForeignKey(e => e.ActorUserId)
                .HasConstraintName("fk_audit_logs_actor_user")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // FK結合用に自動生成される索引に、DDLの命名規則に沿った名前を明示的に付与する
        // (元のDDLには記載のない追加索引だが、結合性能のため採用する)
        modelBuilder.Entity<TaskComment>()
            .HasIndex(e => e.UserId)
            .HasDatabaseName("idx_task_comments_user_id");

        modelBuilder.Entity<TaskStatusHistory>()
            .HasIndex(e => e.ChangedBy)
            .HasDatabaseName("idx_task_status_histories_changed_by");

        // DDLでは created_at / updated_at を TIMESTAMP(タイムゾーンなし)としているため、
        // Npgsqlの既定である timestamp with time zone を上書きする
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(e => e.GetProperties())
                     .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
        {
            property.SetColumnType("timestamp without time zone");
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // UpdatedAtを持つEntityが変更された場合、呼び出し側での指定漏れを防ぐため自動的に現在時刻を設定する
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            var updatedAtProperty = entry.Properties
                .FirstOrDefault(p => p.Metadata.Name == nameof(Project.UpdatedAt));
            if (updatedAtProperty is not null)
            {
                // 列は timestamp without time zone のため、Kind=Utcのままだと Npgsql が書き込みを拒否する。
                // 値はUTCのまま、Kindだけを Unspecified にして保存する(created_at のDB既定値もUTC)
                updatedAtProperty.CurrentValue = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
