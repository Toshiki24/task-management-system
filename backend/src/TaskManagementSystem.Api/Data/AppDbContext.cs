using Microsoft.EntityFrameworkCore;
using TaskManagementSystem.Api.Models;

namespace TaskManagementSystem.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskStatusHistory> TaskStatusHistories => Set<TaskStatusHistory>();

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
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired().HasDefaultValue(TaskItemStatus.Todo);
            entity.Property(e => e.Priority).HasMaxLength(30).IsRequired().HasDefaultValue(TaskItemPriority.Medium);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.ProjectId).HasDatabaseName("idx_tasks_project_id");
            entity.HasIndex(e => e.AssigneeId).HasDatabaseName("idx_tasks_assignee_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_tasks_status");
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

            entity.ToTable(tb =>
            {
                tb.HasCheckConstraint("chk_tasks_status", "status IN ('TODO', 'IN_PROGRESS', 'DONE')");
                tb.HasCheckConstraint("chk_tasks_priority", "priority IN ('LOW', 'MEDIUM', 'HIGH')");
            });
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
            entity.Property(e => e.FromStatus).HasMaxLength(30);
            entity.Property(e => e.ToStatus).HasMaxLength(30).IsRequired();
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

            entity.ToTable(tb =>
            {
                tb.HasCheckConstraint(
                    "chk_task_status_histories_from_status",
                    "from_status IS NULL OR from_status IN ('TODO', 'IN_PROGRESS', 'DONE')");
                tb.HasCheckConstraint(
                    "chk_task_status_histories_to_status",
                    "to_status IN ('TODO', 'IN_PROGRESS', 'DONE')");
            });
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
                updatedAtProperty.CurrentValue = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
