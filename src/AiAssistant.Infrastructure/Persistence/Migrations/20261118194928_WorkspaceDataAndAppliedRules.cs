using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceDataAndAppliedRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at",
                table: "chats",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "applied_rules",
                table: "messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "import_key",
                table: "chats",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "chats",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    materials = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspaces", x => x.id);
                    table.UniqueConstraint("AK_workspaces_id_user_id", x => new { x.id, x.user_id });
                    table.ForeignKey(
                        name: "FK_workspaces_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chats_expires_at",
                table: "chats",
                column: "expires_at",
                filter: "expires_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_chats_user_id_import_key",
                table: "chats",
                columns: new[] { "user_id", "import_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chats_workspace_id_user_id",
                table: "chats",
                columns: new[] { "workspace_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_user_id",
                table: "workspaces",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_chats_workspaces_workspace_id_user_id",
                table: "chats",
                columns: new[] { "workspace_id", "user_id" },
                principalTable: "workspaces",
                principalColumns: new[] { "id", "user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chats_workspaces_workspace_id_user_id",
                table: "chats");

            migrationBuilder.DropTable(
                name: "workspaces");

            migrationBuilder.DropIndex(
                name: "IX_chats_expires_at",
                table: "chats");

            migrationBuilder.DropIndex(
                name: "IX_chats_user_id_import_key",
                table: "chats");

            migrationBuilder.DropIndex(
                name: "IX_chats_workspace_id_user_id",
                table: "chats");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "chats");

            migrationBuilder.DropColumn(
                name: "applied_rules",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "import_key",
                table: "chats");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "chats");
        }
    }
}
