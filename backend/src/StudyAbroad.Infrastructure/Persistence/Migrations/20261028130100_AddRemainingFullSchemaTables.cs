using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRemainingFullSchemaTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "forum_restricted_until",
                schema: "app",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "forum_restriction_reason",
                schema: "app",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_birthdate_locked",
                schema: "app",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "application_round",
                schema: "app",
                table: "target_schools",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deadline",
                schema: "app",
                table: "target_schools",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gap_analysis_json",
                schema: "app",
                table: "target_schools",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "study_level",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "general",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "doc_type",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "faq",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            // question, topic, stage, scope, quote, source_type, source_note, review_due_at, version
            // đã được migration UpdateKnowledgeDocumentForRag tạo. Không khai báo lại.

            migrationBuilder.AddColumn<decimal>(
                name: "cost_usd",
                schema: "app",
                table: "ai_calls",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "fallback_used",
                schema: "app",
                table: "ai_calls",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "model_version",
                schema: "app",
                table: "ai_calls",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plan_code",
                schema: "app",
                table: "ai_calls",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "skill_version",
                schema: "app",
                table: "ai_calls",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "total_tokens",
                schema: "app",
                table: "ai_calls",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "center_reviews",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "visible"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_center_reviews", x => x.id);
                    table.CheckConstraint("ck_center_reviews_rating", "rating >= 1 AND rating <= 5");
                    table.ForeignKey(
                        name: "fk_center_reviews_leads_lead_id",
                        column: x => x.lead_id,
                        principalSchema: "app",
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_center_reviews_study_centers_study_center_id",
                        column: x => x.study_center_id,
                        principalSchema: "app",
                        principalTable: "study_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_center_reviews_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "data_import_batches",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    data_year = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    total_rows = table.Column<int>(type: "integer", nullable: false),
                    passed_rows = table.Column<int>(type: "integer", nullable: false),
                    flagged_rows = table.Column<int>(type: "integer", nullable: false),
                    checked_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: "USAS Research Team"),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "verified"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_import_batches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expert_rules",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rule_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    degree_level = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "all"),
                    target_direction = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    condition_summary = table.Column<string>(type: "text", nullable: true),
                    condition_json = table.Column<string>(type: "jsonb", nullable: true),
                    action_recommendation = table.Column<string>(type: "text", nullable: true),
                    supporting_centers_count = table.Column<int>(type: "integer", nullable: false),
                    source_reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    certainty_factor = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false, defaultValue: 0.90m),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "active"),
                    version = table.Column<int>(type: "integer", nullable: false),
                    verified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: "USAS Advisory Board"),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expert_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "field_evidence",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    field_value = table.Column<string>(type: "text", nullable: true),
                    source_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_section = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    extracted_quote = table.Column<string>(type: "text", nullable: true),
                    verified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: "USAS Audit Panel"),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "verified"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_evidence", x => x.id);
                    table.ForeignKey(
                        name: "fk_field_evidence_universities_university_id",
                        column: x => x.university_id,
                        principalSchema: "app",
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lead_messages",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_flagged = table.Column<bool>(type: "boolean", nullable: false),
                    flag_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "visible"),
                    removed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lead_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_lead_messages_leads_lead_id",
                        column: x => x.lead_id,
                        principalSchema: "app",
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lead_messages_users_removed_by_user_id",
                        column: x => x.removed_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lead_messages_users_sender_user_id",
                        column: x => x.sender_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "policy_alerts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "visa"),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    source_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "info"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policy_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_items",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    tier_ai = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    tier_rule = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    is_tier_divergent = table.Column<bool>(type: "boolean", nullable: false),
                    match_strength = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    rule_codes_triggered = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    financial_fit_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "affordable"),
                    gap_reasons = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendation_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_recommendation_items_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalSchema: "app",
                        principalTable: "recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recommendation_items_universities_university_id",
                        column: x => x.university_id,
                        principalSchema: "app",
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "state_living_costs",
                schema: "app",
                columns: table => new
                {
                    state_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    state_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    index_score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 100.0m),
                    avg_annual_living_usd = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    retrieved_at = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_state_living_costs", x => x.state_code);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    price_monthly_vnd = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    ai_chat_daily_quota = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    max_leads_quota = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_daily_usages",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usage_date = table.Column<DateOnly>(type: "date", nullable: false),
                    ai_chat_count = table.Column<int>(type: "integer", nullable: false),
                    leads_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_daily_usages", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_daily_usages_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "what_if_simulations",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    simulated_profile_json = table.Column<string>(type: "jsonb", nullable: true),
                    simulated_recommendations_json = table.Column<string>(type: "jsonb", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_what_if_simulations", x => x.id);
                    table.ForeignKey(
                        name: "fk_what_if_simulations_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalSchema: "app",
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_what_if_simulations_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_subscriptions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "active"),
                    auto_renew = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_subscriptions_subscription_plans_subscription_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "app",
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_subscription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_vnd = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    gateway = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    gateway_transaction_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "pending"),
                    payload_json = table.Column<string>(type: "text", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_payment_transactions_user_subscriptions_user_subscription_id",
                        column: x => x.user_subscription_id,
                        principalSchema: "app",
                        principalTable: "user_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_payment_transactions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_center_reviews_lead_id",
                schema: "app",
                table: "center_reviews",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_reviews_study_center_id",
                schema: "app",
                table: "center_reviews",
                column: "study_center_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_reviews_user_id",
                schema: "app",
                table: "center_reviews",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_data_import_batches_batch_code",
                schema: "app",
                table: "data_import_batches",
                column: "batch_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expert_rules_degree_level",
                schema: "app",
                table: "expert_rules",
                column: "degree_level");

            migrationBuilder.CreateIndex(
                name: "ix_expert_rules_rule_code",
                schema: "app",
                table: "expert_rules",
                column: "rule_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_field_evidence_university_id",
                schema: "app",
                table: "field_evidence",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "ix_lead_messages_lead_id",
                schema: "app",
                table: "lead_messages",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_lead_messages_removed_by_user_id",
                schema: "app",
                table: "lead_messages",
                column: "removed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_lead_messages_sender_user_id",
                schema: "app",
                table: "lead_messages",
                column: "sender_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_user_id",
                schema: "app",
                table: "payment_transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_user_subscription_id",
                schema: "app",
                table: "payment_transactions",
                column: "user_subscription_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_items_recommendation_id",
                schema: "app",
                table: "recommendation_items",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_items_university_id",
                schema: "app",
                table: "recommendation_items",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_plans_code",
                schema: "app",
                table: "subscription_plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_daily_usages_user_id_usage_date",
                schema: "app",
                table: "user_daily_usages",
                columns: new[] { "user_id", "usage_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_subscriptions_subscription_plan_id",
                schema: "app",
                table: "user_subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_subscriptions_user_id",
                schema: "app",
                table: "user_subscriptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_what_if_simulations_student_profile_id",
                schema: "app",
                table: "what_if_simulations",
                column: "student_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_what_if_simulations_user_id",
                schema: "app",
                table: "what_if_simulations",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "center_reviews",
                schema: "app");

            migrationBuilder.DropTable(
                name: "data_import_batches",
                schema: "app");

            migrationBuilder.DropTable(
                name: "expert_rules",
                schema: "app");

            migrationBuilder.DropTable(
                name: "field_evidence",
                schema: "app");

            migrationBuilder.DropTable(
                name: "lead_messages",
                schema: "app");

            migrationBuilder.DropTable(
                name: "payment_transactions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "policy_alerts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "recommendation_items",
                schema: "app");

            migrationBuilder.DropTable(
                name: "state_living_costs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_daily_usages",
                schema: "app");

            migrationBuilder.DropTable(
                name: "what_if_simulations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_subscriptions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "subscription_plans",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "forum_restricted_until",
                schema: "app",
                table: "users");

            migrationBuilder.DropColumn(
                name: "forum_restriction_reason",
                schema: "app",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_birthdate_locked",
                schema: "app",
                table: "users");

            migrationBuilder.DropColumn(
                name: "application_round",
                schema: "app",
                table: "target_schools");

            migrationBuilder.DropColumn(
                name: "deadline",
                schema: "app",
                table: "target_schools");

            migrationBuilder.DropColumn(
                name: "gap_analysis_json",
                schema: "app",
                table: "target_schools");

            migrationBuilder.DropColumn(
                name: "cost_usd",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.DropColumn(
                name: "fallback_used",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.DropColumn(
                name: "model_version",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.DropColumn(
                name: "plan_code",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.DropColumn(
                name: "skill_version",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.DropColumn(
                name: "total_tokens",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.AlterColumn<string>(
                name: "study_level",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "general");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldDefaultValue: "draft");

            migrationBuilder.AlterColumn<string>(
                name: "doc_type",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "faq");
        }
    }
}
