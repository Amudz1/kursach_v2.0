using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AiAssistant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    daily_request_limit = table.Column<int>(type: "integer", nullable: true),
                    max_personas = table.Column<int>(type: "integer", nullable: false),
                    max_memories = table.Column<int>(type: "integer", nullable: false),
                    max_attachment_bytes = table.Column<int>(type: "integer", nullable: false),
                    allowed_models = table.Column<string>(type: "jsonb", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.id);
                    table.CheckConstraint("ck_subscription_plans_limits", "price >= 0 AND duration_days >= 0 AND (daily_request_limit IS NULL OR daily_request_limit >= 0) AND max_personas >= 0 AND max_memories >= 0 AND max_attachment_bytes >= 0");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    normalized_username = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    session_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "personalization_profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    tone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expertise_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    response_format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    response_length = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    about = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    interests = table.Column<string[]>(type: "text[]", nullable: false),
                    auto_memory_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    onboarding_completed = table.Column<bool>(type: "boolean", nullable: false),
                    adaptation_acknowledged_for = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personalization_profiles", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_personalization_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    system_prompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    temperature = table.Column<double>(type: "double precision", nullable: false),
                    default_model = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personas", x => x.id);
                    table.CheckConstraint("ck_personas_temperature", "temperature >= 0 AND temperature <= 2");
                    table.ForeignKey(
                        name: "FK_personas_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscriptions_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    card_mask = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_transactions_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usage_counters",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    requests_count = table.Column<int>(type: "integer", nullable: false),
                    tokens_used = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_counters", x => new { x.user_id, x.date });
                    table.CheckConstraint("ck_usage_counters_values", "requests_count >= 0 AND tokens_used >= 0");
                    table.ForeignKey(
                        name: "FK_usage_counters_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_memories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    normalized_content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_memories", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_memories_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_pinned = table.Column<bool>(type: "boolean", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    summary_until_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_message_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chats", x => x.id);
                    table.ForeignKey(
                        name: "FK_chats_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_chats_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trigger_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: true),
                    completion_tokens = table.Column<int>(type: "integer", nullable: true),
                    context_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    suggestions = table.Column<string[]>(type: "text[]", nullable: false),
                    error_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    follow_up_completed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_messages_chats_chat_id",
                        column: x => x.chat_id,
                        principalTable: "chats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_messages_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content_type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    extracted_text = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_attachments_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "message_feedback",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_feedback", x => x.id);
                    table.CheckConstraint("ck_message_feedback_rating", "rating IN (-1, 1)");
                    table.ForeignKey(
                        name: "FK_message_feedback_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_message_feedback_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "personas",
                columns: new[] { "id", "default_model", "description", "is_active", "name", "owner_id", "system_prompt", "temperature" },
                values: new object[,]
                {
                    { new Guid("f1000000-0000-4000-8000-000000000001"), "deepseek/deepseek-chat", "Работа с вопросами и повседневными задачами", true, "Универсальный помощник", null, "Помогай решать задачи. Когда данных недостаточно, уточняй вводные. Отделяй факты от предположений.", 0.5 },
                    { new Guid("f1000000-0000-4000-8000-000000000002"), "deepseek/deepseek-chat", "Объяснения и упражнения", true, "Преподаватель", null, "Объясняй понятия на уровне пользователя. Давай небольшой пример и проверочный вопрос.", 0.45000000000000001 },
                    { new Guid("f1000000-0000-4000-8000-000000000003"), "deepseek/deepseek-chat", "Проверка корректности и читаемости кода", true, "Ревьюер кода", null, "Проверяй корректность, безопасность и обработку ошибок. Приводи конкретные замечания и способы исправления.", 0.20000000000000001 },
                    { new Guid("f1000000-0000-4000-8000-000000000004"), "deepseek/deepseek-chat", "Перевод с учётом контекста и регистра", true, "Переводчик", null, "Сохраняй смысл, стиль и терминологию исходника. Указывай существенные неоднозначности.", 0.25 },
                    { new Guid("f1000000-0000-4000-8000-000000000005"), "deepseek/deepseek-chat", "Структура, ясность и редактура", true, "Редактор текста", null, "Исправляй текст, сохраняя авторский смысл. Кратко объясняй значимые редакторские правки.", 0.34999999999999998 }
                });

            migrationBuilder.InsertData(
                table: "subscription_plans",
                columns: new[] { "id", "allowed_models", "code", "daily_request_limit", "description", "duration_days", "is_active", "max_attachment_bytes", "max_memories", "max_personas", "name", "price", "sort_order" },
                values: new object[,]
                {
                    { new Guid("f2000000-0000-4000-8000-000000000001"), "[\"deepseek/deepseek-chat\"]", "Free", 12, "Свободный доступ", 0, true, 2097152, 20, 1, "Свободный доступ", 0m, 1 },
                    { new Guid("f2000000-0000-4000-8000-000000000002"), "[\"deepseek/deepseek-chat\",\"openai/gpt-4o-mini\"]", "Student", 60, "Для учёбы", 30, true, 6291456, 100, 5, "Для учёбы", 199m, 2 },
                    { new Guid("f2000000-0000-4000-8000-000000000003"), "[\"deepseek/deepseek-chat\",\"openai/gpt-4o-mini\"]", "Pro", 250, "Для регулярной работы", 30, true, 10485760, 300, 15, "Для регулярной работы", 399m, 3 },
                    { new Guid("f2000000-0000-4000-8000-000000000004"), "[\"deepseek/deepseek-chat\",\"openai/gpt-4o-mini\"]", "Premium", null, "Без дневного лимита", 30, true, 16777216, 1000, 30, "Без дневного лимита", 699m, 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_attachments_message_id",
                table: "attachments",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_storage_key",
                table: "attachments",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chats_persona_id",
                table: "chats",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_chats_user_id_last_message_at_id",
                table: "chats",
                columns: new[] { "user_id", "last_message_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_message_feedback_message_id_user_id",
                table: "message_feedback",
                columns: new[] { "message_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_feedback_user_id_created_at",
                table: "message_feedback",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_active_generation",
                table: "messages",
                column: "chat_id",
                unique: true,
                filter: "role = 'Assistant' AND status IN ('Pending', 'Streaming')");

            migrationBuilder.CreateIndex(
                name: "IX_messages_chat_id_created_at_id",
                table: "messages",
                columns: new[] { "chat_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_messages_chat_id_request_id",
                table: "messages",
                columns: new[] { "chat_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_messages_persona_id",
                table: "messages",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_personas_owner_id",
                table: "personas",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id_family_id",
                table: "refresh_tokens",
                columns: new[] { "user_id", "family_id" });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plans_code",
                table: "subscription_plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_plan_id",
                table: "subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_user_id",
                table: "subscriptions",
                column: "user_id",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_plan_id",
                table: "transactions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_user_id_request_id",
                table: "transactions",
                columns: new[] { "user_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_memories_user_id_normalized_content",
                table: "user_memories",
                columns: new[] { "user_id", "normalized_content" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_normalized_username",
                table: "users",
                column: "normalized_username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "message_feedback");

            migrationBuilder.DropTable(
                name: "personalization_profiles");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "usage_counters");

            migrationBuilder.DropTable(
                name: "user_memories");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropTable(
                name: "chats");

            migrationBuilder.DropTable(
                name: "personas");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
