using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TokenBudgetsAndTaskRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_subscription_plans_limits",
                table: "subscription_plans");

            migrationBuilder.AddColumn<long>(
                name: "daily_token_limit",
                table: "subscription_plans",
                type: "bigint",
                nullable: true);

            // Keep edited and custom plans, including unlimited plans.
            migrationBuilder.Sql("UPDATE subscription_plans SET daily_token_limit = daily_request_limit::bigint * 2048");
            migrationBuilder.DropColumn("daily_request_limit", "subscription_plans");

            migrationBuilder.AddColumn<bool>(
                name: "adapt_to_task",
                table: "personalization_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "goal",
                table: "personalization_profiles",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "task_rules_json",
                table: "personalization_profiles",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "use_memory_in_replies",
                table: "personalization_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "reserved_tokens",
                table: "messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscription_plans_limits",
                table: "subscription_plans",
                sql: "price >= 0 AND duration_days >= 0 AND (daily_token_limit IS NULL OR daily_token_limit >= 0) AND max_personas >= 0 AND max_memories >= 0 AND max_attachment_bytes >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_subscription_plans_limits",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "adapt_to_task",
                table: "personalization_profiles");

            migrationBuilder.DropColumn(
                name: "goal",
                table: "personalization_profiles");

            migrationBuilder.DropColumn(
                name: "task_rules_json",
                table: "personalization_profiles");

            migrationBuilder.DropColumn(
                name: "use_memory_in_replies",
                table: "personalization_profiles");

            migrationBuilder.DropColumn(
                name: "reserved_tokens",
                table: "messages");

            migrationBuilder.AddColumn<int>(
                name: "daily_request_limit",
                table: "subscription_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("UPDATE subscription_plans SET daily_request_limit = LEAST(daily_token_limit / 2048, 2147483647)::integer WHERE daily_token_limit IS NOT NULL");
            migrationBuilder.DropColumn("daily_token_limit", "subscription_plans");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscription_plans_limits",
                table: "subscription_plans",
                sql: "price >= 0 AND duration_days >= 0 AND (daily_request_limit IS NULL OR daily_request_limit >= 0) AND max_personas >= 0 AND max_memories >= 0 AND max_attachment_bytes >= 0");
        }
    }
}
