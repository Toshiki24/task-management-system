using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagementSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtendActivityVerbsForGit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_activities_verb",
                table: "activities");

            migrationBuilder.AddCheckConstraint(
                name: "chk_activities_verb",
                table: "activities",
                sql: "verb IN ('CREATED', 'UPDATED', 'MOVED', 'COMMENTED', 'GIT_BRANCH_CREATED', 'GIT_PR_OPENED', 'GIT_PR_MERGED', 'GIT_PR_CLOSED', 'GIT_COMMIT_LINKED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_activities_verb",
                table: "activities");

            migrationBuilder.AddCheckConstraint(
                name: "chk_activities_verb",
                table: "activities",
                sql: "verb IN ('CREATED', 'UPDATED', 'MOVED', 'COMMENTED')");
        }
    }
}
