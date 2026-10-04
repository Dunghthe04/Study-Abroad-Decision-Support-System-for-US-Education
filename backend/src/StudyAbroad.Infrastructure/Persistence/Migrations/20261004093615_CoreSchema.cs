using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "app",
                table: "study_centers",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "app",
                table: "study_centers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                schema: "app",
                table: "study_centers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "app",
                table: "study_centers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verification_status",
                schema: "app",
                table: "study_centers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "unverified");

            migrationBuilder.AddColumn<DateTime>(
                name: "verified_at",
                schema: "app",
                table: "study_centers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "verified_by_user_id",
                schema: "app",
                table: "study_centers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_calls",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: true),
                    completion_tokens = table.Column<int>(type: "integer", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_calls", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_settings",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value_json = table.Column<string>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    entity_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    detail_json = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "consents",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    granted = table.Column<bool>(type: "boolean", nullable: false),
                    policy_version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consents", x => x.id);
                    table.ForeignKey(
                        name: "fk_consents_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eval_cases",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    expected_answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    study_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    expected_sources_json = table.Column<string>(type: "jsonb", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eval_cases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "eval_runs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    model_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_cases = table.Column<int>(type: "integer", nullable: false),
                    passed_cases = table.Column<int>(type: "integer", nullable: false),
                    results_json = table.Column<string>(type: "jsonb", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eval_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "forum_posts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    study_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    is_ai_generated = table.Column<bool>(type: "boolean", nullable: false),
                    is_pinned = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forum_posts", x => x.id);
                    table.ForeignKey(
                        name: "fk_forum_posts_forum_posts_parent_post_id",
                        column: x => x.parent_post_id,
                        principalSchema: "app",
                        principalTable: "forum_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_forum_posts_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_documents",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    source_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    doc_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    study_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    retrieved_at = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    verified_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ingested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_knowledge_documents_users_verified_by_user_id",
                        column: x => x.verified_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    link_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "otp_tokens",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    code_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_otp_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_otp_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roadmap_steps",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    step_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    months_before_start = table.Column<int>(type: "integer", nullable: true),
                    resources_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roadmap_steps", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "skills",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    definition_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "student_profiles",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    current_school = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    current_grade = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    grade_scale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    overall_gpa = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    intended_major = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ielts = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    toefl = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    duolingo = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    sat = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    act = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    gre = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    gmat = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    other_tests_json = table.Column<string>(type: "jsonb", nullable: true),
                    annual_budget_usd = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    funding_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    needs_scholarship = table.Column<bool>(type: "boolean", nullable: false),
                    preferred_states = table.Column<List<string>>(type: "text[]", nullable: false),
                    roadmap_progress_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_profiles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "universities",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    city = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    website = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    control = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    acceptance_rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    international_student_count = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_universities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leads",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    study_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    shared_profile = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leads", x => x.id);
                    table.ForeignKey(
                        name: "fk_leads_consents_consent_id",
                        column: x => x.consent_id,
                        principalSchema: "app",
                        principalTable: "consents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_leads_study_centers_study_center_id",
                        column: x => x.study_center_id,
                        principalSchema: "app",
                        principalTable: "study_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leads_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "forum_reports",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    forum_post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    handled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    handled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forum_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_forum_reports_forum_posts_forum_post_id",
                        column: x => x.forum_post_id,
                        principalSchema: "app",
                        principalTable: "forum_posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_forum_reports_users_handled_by_user_id",
                        column: x => x.handled_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_forum_reports_users_reporter_user_id",
                        column: x => x.reporter_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chat_sessions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    study_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    current_step = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_chat_sessions_skills_skill_id",
                        column: x => x.skill_id,
                        principalSchema: "app",
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_chat_sessions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_results",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: false),
                    model_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analysis_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_analysis_results_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalSchema: "app",
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_activities",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    organization = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    role = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile_activities", x => x.id);
                    table.ForeignKey(
                        name: "fk_profile_activities_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalSchema: "app",
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendations",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    study_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    criteria_json = table.Column<string>(type: "jsonb", nullable: false),
                    items_json = table.Column<string>(type: "jsonb", nullable: false),
                    algorithm_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "fk_recommendations_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalSchema: "app",
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_recommendations_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transcript_scores",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    term_order = table.Column<int>(type: "integer", nullable: false),
                    subject = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    credits = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transcript_scores", x => x.id);
                    table.ForeignKey(
                        name: "fk_transcript_scores_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalSchema: "app",
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scholarships",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    provider = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    study_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount_usd = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    coverage_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    min_gpa4 = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    min_ielts = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    eligibility_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    retrieved_at = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scholarships", x => x.id);
                    table.ForeignKey(
                        name: "fk_scholarships_universities_university_id",
                        column: x => x.university_id,
                        principalSchema: "app",
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "target_schools",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_target_schools", x => x.id);
                    table.ForeignKey(
                        name: "fk_target_schools_universities_university_id",
                        column: x => x.university_id,
                        principalSchema: "app",
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_target_schools_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "university_offerings",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    majors = table.Column<List<string>>(type: "text[]", nullable: false),
                    stem_majors = table.Column<List<string>>(type: "text[]", nullable: false),
                    academic_year = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    tuition_usd = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    living_usd = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    fees_usd = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    min_gpa4 = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    min_ielts = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: true),
                    min_toefl = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    min_duolingo = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    sat_policy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    deadlines_json = table.Column<string>(type: "jsonb", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    retrieved_at = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_university_offerings", x => x.id);
                    table.ForeignKey(
                        name: "fk_university_offerings_universities_university_id",
                        column: x => x.university_id,
                        principalSchema: "app",
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    step = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    sources_json = table.Column<string>(type: "jsonb", nullable: true),
                    suggested_centers_json = table.Column<string>(type: "jsonb", nullable: true),
                    review_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_chat_messages_chat_sessions_chat_session_id",
                        column: x => x.chat_session_id,
                        principalSchema: "app",
                        principalTable: "chat_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_study_centers_owner_user_id",
                schema: "app",
                table: "study_centers",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_study_centers_verified_by_user_id",
                schema: "app",
                table: "study_centers",
                column: "verified_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_calls_created_at",
                schema: "app",
                table: "ai_calls",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_analysis_results_student_profile_id_kind_created_at",
                schema: "app",
                table: "analysis_results",
                columns: new[] { "student_profile_id", "kind", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_app_settings_key",
                schema: "app",
                table: "app_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_user_id",
                schema: "app",
                table: "audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created_at",
                schema: "app",
                table: "audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_chat_session_id_created_at",
                schema: "app",
                table: "chat_messages",
                columns: new[] { "chat_session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_review_status",
                schema: "app",
                table: "chat_messages",
                column: "review_status");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_skill_id",
                schema: "app",
                table: "chat_sessions",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_user_id_created_at",
                schema: "app",
                table: "chat_sessions",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_consents_user_id_purpose",
                schema: "app",
                table: "consents",
                columns: new[] { "user_id", "purpose" });

            migrationBuilder.CreateIndex(
                name: "ix_eval_runs_started_at",
                schema: "app",
                table: "eval_runs",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_forum_posts_parent_post_id_created_at",
                schema: "app",
                table: "forum_posts",
                columns: new[] { "parent_post_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_forum_posts_study_level_status",
                schema: "app",
                table: "forum_posts",
                columns: new[] { "study_level", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_forum_posts_user_id",
                schema: "app",
                table: "forum_posts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_forum_reports_forum_post_id",
                schema: "app",
                table: "forum_reports",
                column: "forum_post_id");

            migrationBuilder.CreateIndex(
                name: "ix_forum_reports_handled_by_user_id",
                schema: "app",
                table: "forum_reports",
                column: "handled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_forum_reports_reporter_user_id",
                schema: "app",
                table: "forum_reports",
                column: "reporter_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_forum_reports_status",
                schema: "app",
                table: "forum_reports",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_status_study_level_doc_type",
                schema: "app",
                table: "knowledge_documents",
                columns: new[] { "status", "study_level", "doc_type" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_verified_by_user_id",
                schema: "app",
                table: "knowledge_documents",
                column: "verified_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_consent_id",
                schema: "app",
                table: "leads",
                column: "consent_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_study_center_id_status",
                schema: "app",
                table: "leads",
                columns: new[] { "study_center_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_user_id",
                schema: "app",
                table: "leads",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_read_at",
                schema: "app",
                table: "notifications",
                columns: new[] { "user_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "ix_otp_tokens_user_id_purpose",
                schema: "app",
                table: "otp_tokens",
                columns: new[] { "user_id", "purpose" });

            migrationBuilder.CreateIndex(
                name: "ix_profile_activities_student_profile_id_kind",
                schema: "app",
                table: "profile_activities",
                columns: new[] { "student_profile_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_student_profile_id",
                schema: "app",
                table: "recommendations",
                column: "student_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_user_id_created_at",
                schema: "app",
                table: "recommendations",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_roadmap_steps_study_level_step_key",
                schema: "app",
                table: "roadmap_steps",
                columns: new[] { "study_level", "step_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scholarships_study_level",
                schema: "app",
                table: "scholarships",
                column: "study_level");

            migrationBuilder.CreateIndex(
                name: "ix_scholarships_university_id",
                schema: "app",
                table: "scholarships",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "ix_skills_key_version",
                schema: "app",
                table: "skills",
                columns: new[] { "key", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_user_id",
                schema: "app",
                table: "student_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_target_schools_university_id",
                schema: "app",
                table: "target_schools",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "ix_target_schools_user_id_university_id",
                schema: "app",
                table: "target_schools",
                columns: new[] { "user_id", "university_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transcript_scores_student_profile_id_term_order",
                schema: "app",
                table: "transcript_scores",
                columns: new[] { "student_profile_id", "term_order" });

            migrationBuilder.CreateIndex(
                name: "ix_universities_code",
                schema: "app",
                table: "universities",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_universities_state",
                schema: "app",
                table: "universities",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "ix_university_offerings_university_id_study_level",
                schema: "app",
                table: "university_offerings",
                columns: new[] { "university_id", "study_level" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_study_centers_users_owner_user_id",
                schema: "app",
                table: "study_centers",
                column: "owner_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_study_centers_users_verified_by_user_id",
                schema: "app",
                table: "study_centers",
                column: "verified_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_study_centers_users_owner_user_id",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropForeignKey(
                name: "fk_study_centers_users_verified_by_user_id",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropTable(
                name: "ai_calls",
                schema: "app");

            migrationBuilder.DropTable(
                name: "analysis_results",
                schema: "app");

            migrationBuilder.DropTable(
                name: "app_settings",
                schema: "app");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "chat_messages",
                schema: "app");

            migrationBuilder.DropTable(
                name: "eval_cases",
                schema: "app");

            migrationBuilder.DropTable(
                name: "eval_runs",
                schema: "app");

            migrationBuilder.DropTable(
                name: "forum_reports",
                schema: "app");

            migrationBuilder.DropTable(
                name: "knowledge_documents",
                schema: "app");

            migrationBuilder.DropTable(
                name: "leads",
                schema: "app");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "app");

            migrationBuilder.DropTable(
                name: "otp_tokens",
                schema: "app");

            migrationBuilder.DropTable(
                name: "profile_activities",
                schema: "app");

            migrationBuilder.DropTable(
                name: "recommendations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "roadmap_steps",
                schema: "app");

            migrationBuilder.DropTable(
                name: "scholarships",
                schema: "app");

            migrationBuilder.DropTable(
                name: "target_schools",
                schema: "app");

            migrationBuilder.DropTable(
                name: "transcript_scores",
                schema: "app");

            migrationBuilder.DropTable(
                name: "university_offerings",
                schema: "app");

            migrationBuilder.DropTable(
                name: "chat_sessions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "forum_posts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "consents",
                schema: "app");

            migrationBuilder.DropTable(
                name: "student_profiles",
                schema: "app");

            migrationBuilder.DropTable(
                name: "universities",
                schema: "app");

            migrationBuilder.DropTable(
                name: "skills",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_study_centers_owner_user_id",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropIndex(
                name: "ix_study_centers_verified_by_user_id",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "verification_status",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "verified_at",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "verified_by_user_id",
                schema: "app",
                table: "study_centers");
        }
    }
}
