using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppearanceAndEntityColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "workspaces",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "icon",
                table: "workspaces",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "personas",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "icon",
                table: "personas",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "appearance_settings",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    preset = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "classic"),
                    color_mode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "system"),
                    settings = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appearance_settings", x => x.user_id);
                    table.CheckConstraint("ck_appearance_settings_color_mode", "color_mode IN ('system', 'light', 'dark')");
                    table.CheckConstraint("ck_appearance_settings_size", "pg_column_size(settings) <= 8192");
                    table.ForeignKey(
                        name: "FK_appearance_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "personas",
                keyColumn: "id",
                keyValue: new Guid("f1000000-0000-4000-8000-000000000001"),
                columns: new[] { "color", "icon" },
                values: new object[] { "cat-1", "sparkles" });

            migrationBuilder.UpdateData(
                table: "personas",
                keyColumn: "id",
                keyValue: new Guid("f1000000-0000-4000-8000-000000000002"),
                columns: new[] { "color", "icon" },
                values: new object[] { "cat-2", "graduation" });

            migrationBuilder.UpdateData(
                table: "personas",
                keyColumn: "id",
                keyValue: new Guid("f1000000-0000-4000-8000-000000000003"),
                columns: new[] { "color", "icon" },
                values: new object[] { "cat-3", "code" });

            migrationBuilder.UpdateData(
                table: "personas",
                keyColumn: "id",
                keyValue: new Guid("f1000000-0000-4000-8000-000000000004"),
                columns: new[] { "color", "icon" },
                values: new object[] { "cat-4", "globe" });

            migrationBuilder.UpdateData(
                table: "personas",
                keyColumn: "id",
                keyValue: new Guid("f1000000-0000-4000-8000-000000000005"),
                columns: new[] { "color", "icon" },
                values: new object[] { "cat-5", "pen" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_workspaces_color",
                table: "workspaces",
                sql: "color IN ('cat-1','cat-2','cat-3','cat-4','cat-5','cat-6','cat-7','cat-8')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_personas_color",
                table: "personas",
                sql: "color IN ('cat-1','cat-2','cat-3','cat-4','cat-5','cat-6','cat-7','cat-8')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appearance_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_workspaces_color",
                table: "workspaces");

            migrationBuilder.DropCheckConstraint(
                name: "ck_personas_color",
                table: "personas");

            migrationBuilder.DropColumn(
                name: "color",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "icon",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "color",
                table: "personas");

            migrationBuilder.DropColumn(
                name: "icon",
                table: "personas");
        }
    }
}
