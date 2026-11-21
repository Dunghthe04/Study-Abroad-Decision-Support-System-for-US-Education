using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gói dịch vụ v2: định hình lại 3 bảng thanh toán chưa dùng (chưa có dòng nào), thêm 2 bảng gói Gia đình
    /// và cột ai_calls.user_subscription_id. user_subscriptions.expires_at thay bằng ends_at (null khi kỳ đang xếp hàng).
    /// Trigger trg_payment_transactions_require_user giữ nguyên.
    /// </summary>
    public partial class AddSubscriptionBillingV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_subscriptions_subscription_plans_subscription_plan_id",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropIndex(
                name: "ix_user_subscriptions_user_id",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "auto_renew",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "expires_at",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "ai_chat_daily_quota",
                schema: "app",
                table: "subscription_plans");

            migrationBuilder.RenameColumn(
                name: "price_monthly_vnd",
                schema: "app",
                table: "subscription_plans",
                newName: "price_vnd");

            migrationBuilder.RenameColumn(
                name: "max_leads_quota",
                schema: "app",
                table: "subscription_plans",
                newName: "estimated_questions");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "app",
                table: "user_subscriptions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "active");

            migrationBuilder.AlterColumn<DateTime>(
                name: "starts_at",
                schema: "app",
                table: "user_subscriptions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<Guid>(
                name: "activated_from_order_id",
                schema: "app",
                table: "user_subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duration_days",
                schema: "app",
                table: "user_subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ended_at",
                schema: "app",
                table: "user_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ended_reason",
                schema: "app",
                table: "user_subscriptions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ends_at",
                schema: "app",
                table: "user_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "exhausted_notified_at",
                schema: "app",
                table: "user_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "family_group_id",
                schema: "app",
                table: "user_subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "app",
                table: "user_subscriptions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "plan_name",
                schema: "app",
                table: "user_subscriptions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "price_vnd",
                schema: "app",
                table: "user_subscriptions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "reminder_sent_at",
                schema: "app",
                table: "user_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "token_quota",
                schema: "app",
                table: "user_subscriptions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "warned_80_at",
                schema: "app",
                table: "user_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "duration_days",
                schema: "app",
                table: "subscription_plans",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                schema: "app",
                table: "subscription_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "app",
                table: "subscription_plans",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "token_quota",
                schema: "app",
                table: "subscription_plans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "admin_note",
                schema: "app",
                table: "payment_transactions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "checkout_url",
                schema: "app",
                table: "payment_transactions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "app",
                table: "payment_transactions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at",
                schema: "app",
                table: "payment_transactions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "handled_by_user_id",
                schema: "app",
                table: "payment_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "order_code",
                schema: "app",
                table: "payment_transactions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "plan_duration_days",
                schema: "app",
                table: "payment_transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "plan_id",
                schema: "app",
                table: "payment_transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "plan_kind",
                schema: "app",
                table: "payment_transactions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "plan_name",
                schema: "app",
                table: "payment_transactions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "plan_token_quota",
                schema: "app",
                table: "payment_transactions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "qr_code",
                schema: "app",
                table: "payment_transactions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "user_subscription_id",
                schema: "app",
                table: "ai_calls",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "family_groups",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ended_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_family_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_family_groups_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "family_members",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invited_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    seat_role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    invite_sent_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_invite_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    invited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ended_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_family_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_family_members_family_groups_family_group_id",
                        column: x => x.family_group_id,
                        principalSchema: "app",
                        principalTable: "family_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_family_members_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_subscriptions_family_group_id",
                schema: "app",
                table: "user_subscriptions",
                column: "family_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_subscriptions_user_id_kind_status",
                schema: "app",
                table: "user_subscriptions",
                columns: new[] { "user_id", "kind", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_user_subscriptions_one_active_per_lane",
                schema: "app",
                table: "user_subscriptions",
                columns: new[] { "user_id", "kind" },
                unique: true,
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "ux_user_subscriptions_one_queued_per_lane",
                schema: "app",
                table: "user_subscriptions",
                columns: new[] { "user_id", "kind" },
                unique: true,
                filter: "status = 'queued'");

            migrationBuilder.CreateIndex(
                name: "ux_subscription_plans_single_free",
                schema: "app",
                table: "subscription_plans",
                column: "kind",
                unique: true,
                filter: "kind = 'free'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscription_plans_duration",
                schema: "app",
                table: "subscription_plans",
                sql: "kind = 'free' OR duration_days IN (30, 90)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscription_plans_kind",
                schema: "app",
                table: "subscription_plans",
                sql: "kind IN ('free', 'personal', 'family')");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_handled_by_user_id",
                schema: "app",
                table: "payment_transactions",
                column: "handled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_order_code",
                schema: "app",
                table: "payment_transactions",
                column: "order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_subscription_plan_id",
                schema: "app",
                table: "payment_transactions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ux_payment_transactions_one_pending_per_user",
                schema: "app",
                table: "payment_transactions",
                column: "user_id",
                unique: true,
                filter: "status = 'pending'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payment_transactions_status",
                schema: "app",
                table: "payment_transactions",
                sql: "status IN ('pending', 'paid', 'mismatch', 'expired', 'cancelled')");

            migrationBuilder.CreateIndex(
                name: "ix_ai_calls_user_subscription_id_created_at",
                schema: "app",
                table: "ai_calls",
                columns: new[] { "user_subscription_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_family_groups_one_active_per_owner",
                schema: "app",
                table: "family_groups",
                column: "owner_user_id",
                unique: true,
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "ix_family_members_family_group_id_status",
                schema: "app",
                table: "family_members",
                columns: new[] { "family_group_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_family_members_token_hash",
                schema: "app",
                table: "family_members",
                column: "token_hash",
                unique: true,
                filter: "token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_family_members_one_active_per_user",
                schema: "app",
                table: "family_members",
                column: "user_id",
                unique: true,
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "ux_family_members_one_pending_per_email",
                schema: "app",
                table: "family_members",
                columns: new[] { "family_group_id", "invited_email" },
                unique: true,
                filter: "status = 'pending'");

            migrationBuilder.AddForeignKey(
                name: "fk_ai_calls_user_subscriptions_user_subscription_id",
                schema: "app",
                table: "ai_calls",
                column: "user_subscription_id",
                principalSchema: "app",
                principalTable: "user_subscriptions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_payment_transactions_subscription_plans_subscription_plan_id",
                schema: "app",
                table: "payment_transactions",
                column: "plan_id",
                principalSchema: "app",
                principalTable: "subscription_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_payment_transactions_users_handled_by_user_id",
                schema: "app",
                table: "payment_transactions",
                column: "handled_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_user_subscriptions_family_groups_family_group_id",
                schema: "app",
                table: "user_subscriptions",
                column: "family_group_id",
                principalSchema: "app",
                principalTable: "family_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_user_subscriptions_subscription_plans_subscription_plan_id",
                schema: "app",
                table: "user_subscriptions",
                column: "plan_id",
                principalSchema: "app",
                principalTable: "subscription_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ai_calls_user_subscriptions_user_subscription_id",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.DropForeignKey(
                name: "fk_payment_transactions_subscription_plans_subscription_plan_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropForeignKey(
                name: "fk_payment_transactions_users_handled_by_user_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropForeignKey(
                name: "fk_user_subscriptions_family_groups_family_group_id",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropForeignKey(
                name: "fk_user_subscriptions_subscription_plans_subscription_plan_id",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropTable(
                name: "family_members",
                schema: "app");

            migrationBuilder.DropTable(
                name: "family_groups",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_user_subscriptions_family_group_id",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropIndex(
                name: "ix_user_subscriptions_user_id_kind_status",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropIndex(
                name: "ux_user_subscriptions_one_active_per_lane",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropIndex(
                name: "ux_user_subscriptions_one_queued_per_lane",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropIndex(
                name: "ux_subscription_plans_single_free",
                schema: "app",
                table: "subscription_plans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscription_plans_duration",
                schema: "app",
                table: "subscription_plans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscription_plans_kind",
                schema: "app",
                table: "subscription_plans");

            migrationBuilder.DropIndex(
                name: "ix_payment_transactions_handled_by_user_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropIndex(
                name: "ix_payment_transactions_order_code",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropIndex(
                name: "ix_payment_transactions_subscription_plan_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropIndex(
                name: "ux_payment_transactions_one_pending_per_user",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payment_transactions_status",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropIndex(
                name: "ix_ai_calls_user_subscription_id_created_at",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.DropColumn(
                name: "activated_from_order_id",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "duration_days",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "ended_at",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "ended_reason",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "ends_at",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "exhausted_notified_at",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "family_group_id",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "kind",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "plan_name",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "price_vnd",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "reminder_sent_at",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "token_quota",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "warned_80_at",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "display_order",
                schema: "app",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "kind",
                schema: "app",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "token_quota",
                schema: "app",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "admin_note",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "checkout_url",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "expires_at",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "handled_by_user_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "order_code",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "plan_duration_days",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "plan_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "plan_kind",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "plan_name",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "plan_token_quota",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "qr_code",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "user_subscription_id",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.RenameColumn(
                name: "price_vnd",
                schema: "app",
                table: "subscription_plans",
                newName: "price_monthly_vnd");

            migrationBuilder.RenameColumn(
                name: "estimated_questions",
                schema: "app",
                table: "subscription_plans",
                newName: "max_leads_quota");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "app",
                table: "user_subscriptions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "active",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<DateTime>(
                name: "starts_at",
                schema: "app",
                table: "user_subscriptions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "auto_renew",
                schema: "app",
                table: "user_subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at",
                schema: "app",
                table: "user_subscriptions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<int>(
                name: "duration_days",
                schema: "app",
                table: "subscription_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ai_chat_daily_quota",
                schema: "app",
                table: "subscription_plans",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.CreateIndex(
                name: "ix_user_subscriptions_user_id",
                schema: "app",
                table: "user_subscriptions",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_user_subscriptions_subscription_plans_subscription_plan_id",
                schema: "app",
                table: "user_subscriptions",
                column: "plan_id",
                principalSchema: "app",
                principalTable: "subscription_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
