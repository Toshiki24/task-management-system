using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagementSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskBoardPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_task_status_histories_from_status",
                table: "task_status_histories");

            migrationBuilder.DropCheckConstraint(
                name: "chk_task_status_histories_to_status",
                table: "task_status_histories");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "tasks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "TODO",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "TODO");

            migrationBuilder.AddColumn<double>(
                name: "board_position",
                table: "tasks",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            // 既存タスクの board_position を、状態列ごとに作成順(created_at, id)で 0..n に採番する。
            migrationBuilder.Sql(@"
                UPDATE tasks t
                SET board_position = s.rn - 1
                FROM (
                    SELECT id,
                           ROW_NUMBER() OVER (PARTITION BY project_id, status ORDER BY created_at, id) AS rn
                    FROM tasks
                ) s
                WHERE t.id = s.id;");

            migrationBuilder.AlterColumn<string>(
                name: "to_status",
                table: "task_status_histories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "from_status",
                table: "task_status_histories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_tasks_board_order",
                table: "tasks",
                columns: new[] { "project_id", "status", "board_position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_tasks_board_order",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "board_position",
                table: "tasks");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "tasks",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "TODO",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "TODO");

            migrationBuilder.AlterColumn<string>(
                name: "to_status",
                table: "task_status_histories",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "from_status",
                table: "task_status_histories",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "chk_task_status_histories_from_status",
                table: "task_status_histories",
                sql: "from_status IS NULL OR from_status IN ('TODO', 'IN_PROGRESS', 'DONE')");

            migrationBuilder.AddCheckConstraint(
                name: "chk_task_status_histories_to_status",
                table: "task_status_histories",
                sql: "to_status IN ('TODO', 'IN_PROGRESS', 'DONE')");
        }
    }
}
