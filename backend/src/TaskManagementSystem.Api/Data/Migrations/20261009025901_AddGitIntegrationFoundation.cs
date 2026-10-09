using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TaskManagementSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGitIntegrationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "git_connections",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    workspace_id = table.Column<long>(type: "bigint", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    base_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    auth_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    secret_ref = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    external_account = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_git_connections", x => x.id);
                    table.CheckConstraint("chk_git_connections_auth_type", "auth_type IN ('GITHUB_APP', 'OAUTH', 'PAT', 'GROUP_TOKEN')");
                    table.CheckConstraint("chk_git_connections_provider", "provider IN ('GITHUB', 'GITLAB')");
                    table.CheckConstraint("chk_git_connections_status", "status IN ('ACTIVE', 'DISABLED', 'ERROR')");
                    table.ForeignKey(
                        name: "fk_git_connections_workspace",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "git_identities",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    workspace_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    external_user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_git_identities", x => x.id);
                    table.CheckConstraint("chk_git_identities_provider", "provider IN ('GITHUB', 'GITLAB')");
                    table.ForeignKey(
                        name: "fk_git_identities_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_git_identities_workspace",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transition_rules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    workspace_id = table.Column<long>(type: "bigint", nullable: false),
                    project_id = table.Column<long>(type: "bigint", nullable: true),
                    trigger = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    to_status_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transition_rules", x => x.id);
                    table.CheckConstraint("chk_transition_rules_trigger", "trigger IN ('BRANCH_CREATED', 'PR_OPENED', 'MR_OPENED', 'PR_MERGED', 'MR_MERGED')");
                    table.ForeignKey(
                        name: "fk_transition_rules_project",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_transition_rules_workspace",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repository_links",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    git_connection_id = table.Column<long>(type: "bigint", nullable: false),
                    external_repo_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    repo_full_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    default_branch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repository_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_repository_links_connection",
                        column: x => x.git_connection_id,
                        principalTable: "git_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_repository_links_project",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhook_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    git_connection_id = table.Column<long>(type: "bigint", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    external_event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    signature_verified = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "RECEIVED"),
                    received_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    processed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_events", x => x.id);
                    table.CheckConstraint("chk_webhook_events_status", "status IN ('RECEIVED', 'PROCESSED', 'SKIPPED', 'FAILED')");
                    table.ForeignKey(
                        name: "fk_webhook_events_connection",
                        column: x => x.git_connection_id,
                        principalTable: "git_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_git_links",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_id = table.Column<long>(type: "bigint", nullable: false),
                    repository_link_id = table.Column<long>(type: "bigint", nullable: false),
                    link_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    external_ref = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_git_links", x => x.id);
                    table.CheckConstraint("chk_task_git_links_state", "state IS NULL OR state IN ('OPEN', 'MERGED', 'CLOSED')");
                    table.CheckConstraint("chk_task_git_links_type", "link_type IN ('BRANCH', 'PR', 'MR', 'COMMIT')");
                    table.ForeignKey(
                        name: "fk_task_git_links_repository_link",
                        column: x => x.repository_link_id,
                        principalTable: "repository_links",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_git_links_task",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_git_connections_workspace_id",
                table: "git_connections",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "uq_git_connections_workspace_provider_account",
                table: "git_connections",
                columns: new[] { "workspace_id", "provider", "external_account" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_git_identities_workspace_id",
                table: "git_identities",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ix_git_identities_user_id",
                table: "git_identities",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_git_identities_workspace_provider_user",
                table: "git_identities",
                columns: new[] { "workspace_id", "provider", "external_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_repository_links_project_id",
                table: "repository_links",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "uq_repository_links_connection_repo",
                table: "repository_links",
                columns: new[] { "git_connection_id", "external_repo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_task_git_links_task_id",
                table: "task_git_links",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "uq_task_git_links_repo_type_ref",
                table: "task_git_links",
                columns: new[] { "repository_link_id", "link_type", "external_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_transition_rules_project_id",
                table: "transition_rules",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "idx_transition_rules_workspace_id",
                table: "transition_rules",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "idx_webhook_events_received_at",
                table: "webhook_events",
                column: "received_at");

            migrationBuilder.CreateIndex(
                name: "uq_webhook_events_connection_event",
                table: "webhook_events",
                columns: new[] { "git_connection_id", "external_event_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "git_identities");

            migrationBuilder.DropTable(
                name: "task_git_links");

            migrationBuilder.DropTable(
                name: "transition_rules");

            migrationBuilder.DropTable(
                name: "webhook_events");

            migrationBuilder.DropTable(
                name: "repository_links");

            migrationBuilder.DropTable(
                name: "git_connections");
        }
    }
}
