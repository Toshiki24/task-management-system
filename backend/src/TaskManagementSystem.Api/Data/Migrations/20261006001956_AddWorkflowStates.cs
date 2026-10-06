using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TaskManagementSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflow_states",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    workspace_id = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_states", x => x.id);
                    table.CheckConstraint("chk_workflow_states_category", "category IN ('BACKLOG', 'TODO', 'IN_PROGRESS', 'DONE', 'CANCELLED')");
                    table.ForeignKey(
                        name: "fk_workflow_states_workspace",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_workflow_states_workspace_id",
                table: "workflow_states",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "uq_workflow_states_workspace_key",
                table: "workflow_states",
                columns: new[] { "workspace_id", "key" },
                unique: true);

            // 既存の各ワークスペースに既定ワークフロー(TODO/IN_PROGRESS/DONE)をバックフィルする。
            // これにより既存タスクの status 値がそのまま有効な状態キーになる(M2 §3.1・§12)。
            // 状態が未投入のワークスペースだけを対象にする(再実行しても二重投入しない)。
            migrationBuilder.Sql(@"
                INSERT INTO workflow_states (workspace_id, key, name, category, position, is_default)
                SELECT w.id, v.key, v.name, v.category, v.position, v.is_default
                FROM workspaces w
                CROSS JOIN (VALUES
                    ('TODO', '未着手', 'TODO', 0, true),
                    ('IN_PROGRESS', '対応中', 'IN_PROGRESS', 1, false),
                    ('DONE', '完了', 'DONE', 2, false)
                ) AS v(key, name, category, position, is_default)
                WHERE NOT EXISTS (SELECT 1 FROM workflow_states ws WHERE ws.workspace_id = w.id);");

            // status は WS ごとのワークフローを指す動的な値になったため、固定値の CHECK 制約を外す。
            // 妥当性はサービス層で LINQ により WS の状態集合に対して検証する(M2 §3.2)。
            migrationBuilder.DropCheckConstraint(
                name: "chk_tasks_status",
                table: "tasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflow_states");

            migrationBuilder.AddCheckConstraint(
                name: "chk_tasks_status",
                table: "tasks",
                sql: "status IN ('TODO', 'IN_PROGRESS', 'DONE')");
        }
    }
}
