using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountInterfaceVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "interface_variant",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "current");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_interface_variant",
                table: "users",
                sql: "interface_variant IN ('current', 'focus')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_interface_variant",
                table: "users");

            migrationBuilder.DropColumn(
                name: "interface_variant",
                table: "users");
        }
    }
}
