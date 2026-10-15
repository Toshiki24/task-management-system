using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TaskManagementSystem.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSsoConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sso_connections",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    protocol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "OIDC"),
                    issuer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    client_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    secret_ref = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    scopes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, defaultValue: "openid email profile"),
                    auto_provision = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    provision_workspace_id = table.Column<long>(type: "bigint", nullable: true),
                    provision_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sso_connections", x => x.id);
                    table.CheckConstraint("chk_sso_connections_protocol", "protocol IN ('OIDC')");
                    table.CheckConstraint("chk_sso_connections_provision_role", "provision_role IS NULL OR provision_role IN ('ADMIN', 'MEMBER', 'VIEWER')");
                });

            migrationBuilder.CreateTable(
                name: "sso_auth_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    connection_id = table.Column<long>(type: "bigint", nullable: false),
                    state_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code_verifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nonce = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    redirect_uri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sso_auth_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_sso_auth_requests_connection",
                        column: x => x.connection_id,
                        principalTable: "sso_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sso_auth_requests_connection_id",
                table: "sso_auth_requests",
                column: "connection_id");

            migrationBuilder.CreateIndex(
                name: "uq_sso_auth_requests_state_hash",
                table: "sso_auth_requests",
                column: "state_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sso_auth_requests");

            migrationBuilder.DropTable(
                name: "sso_connections");
        }
    }
}
