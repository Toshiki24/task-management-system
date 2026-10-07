using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TaskManagementSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_dependencies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    blocking_task_id = table.Column<long>(type: "bigint", nullable: false),
                    blocked_task_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_dependencies", x => x.id);
                    table.CheckConstraint("chk_task_dependencies_no_self", "blocking_task_id <> blocked_task_id");
                    table.ForeignKey(
                        name: "fk_task_dependencies_blocked_task",
                        column: x => x.blocked_task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_dependencies_blocking_task",
                        column: x => x.blocking_task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_task_dependencies_blocked_task_id",
                table: "task_dependencies",
                column: "blocked_task_id");

            migrationBuilder.CreateIndex(
                name: "idx_task_dependencies_blocking_task_id",
                table: "task_dependencies",
                column: "blocking_task_id");

            migrationBuilder.CreateIndex(
                name: "uq_task_dependencies_pair",
                table: "task_dependencies",
                columns: new[] { "blocking_task_id", "blocked_task_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_dependencies");
        }
    }
}
