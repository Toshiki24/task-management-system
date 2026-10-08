using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TaskManagementSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "milestone_id",
                table: "tasks",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "milestones",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "OPEN"),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_milestones", x => x.id);
                    table.CheckConstraint("chk_milestones_status", "status IN ('OPEN', 'CLOSED')");
                    table.ForeignKey(
                        name: "fk_milestones_project",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_tasks_milestone_id",
                table: "tasks",
                column: "milestone_id");

            migrationBuilder.CreateIndex(
                name: "idx_milestones_project_id",
                table: "milestones",
                column: "project_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tasks_milestone",
                table: "tasks",
                column: "milestone_id",
                principalTable: "milestones",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tasks_milestone",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "milestones");

            migrationBuilder.DropIndex(
                name: "idx_tasks_milestone_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "milestone_id",
                table: "tasks");
        }
    }
}
