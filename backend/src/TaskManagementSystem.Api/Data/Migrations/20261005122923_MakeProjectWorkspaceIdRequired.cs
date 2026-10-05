using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagementSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeProjectWorkspaceIdRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOT NULL 化の前に、workspace_id が未設定のプロジェクトを既定ワークスペースへ退避する
            // (step1 以降、旧 POST /api/projects で作られた行が NULL のまま残っている可能性があるため)。
            // 無ければ何もしない。
            migrationBuilder.Sql(@"
DO $$
DECLARE
  default_ws_id bigint;
BEGIN
  IF EXISTS (SELECT 1 FROM projects WHERE workspace_id IS NULL) THEN
    SELECT id INTO default_ws_id FROM workspaces WHERE name = 'Default Workspace' ORDER BY id LIMIT 1;
    IF default_ws_id IS NULL THEN
      INSERT INTO workspaces (name, description)
        VALUES ('Default Workspace', 'workspace_id 未設定のプロジェクトの既定ワークスペース')
        RETURNING id INTO default_ws_id;
    END IF;

    UPDATE projects SET workspace_id = default_ws_id WHERE workspace_id IS NULL;

    INSERT INTO workspace_members (workspace_id, user_id, role)
      SELECT DISTINCT default_ws_id, pm.user_id, 'MEMBER'
      FROM project_members pm
      JOIN projects p ON p.id = pm.project_id
      WHERE p.workspace_id = default_ws_id
      ON CONFLICT (workspace_id, user_id) DO NOTHING;
  END IF;
END $$;");

            migrationBuilder.AlterColumn<long>(
                name: "workspace_id",
                table: "projects",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "workspace_id",
                table: "projects",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
