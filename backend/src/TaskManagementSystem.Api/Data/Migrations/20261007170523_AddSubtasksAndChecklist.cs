using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TaskManagementSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubtasksAndChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "parent_task_id",
                table: "tasks",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "task_checklist_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    task_id = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_done = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_checklist_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_checklist_items_task",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_tasks_parent_task_id",
                table: "tasks",
                column: "parent_task_id");

            migrationBuilder.CreateIndex(
                name: "idx_task_checklist_items_task_id",
                table: "task_checklist_items",
                column: "task_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tasks_parent_task",
                table: "tasks",
                column: "parent_task_id",
                principalTable: "tasks",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tasks_parent_task",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "task_checklist_items");

            migrationBuilder.DropIndex(
                name: "idx_tasks_parent_task_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "parent_task_id",
                table: "tasks");
        }
    }
}
