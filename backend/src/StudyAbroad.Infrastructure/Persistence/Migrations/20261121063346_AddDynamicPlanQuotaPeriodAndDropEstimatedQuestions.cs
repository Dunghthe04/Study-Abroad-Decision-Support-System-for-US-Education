using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicPlanQuotaPeriodAndDropEstimatedQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_subscription_plans_duration",
                schema: "app",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "estimated_questions",
                schema: "app",
                table: "subscription_plans");

            migrationBuilder.AddColumn<int>(
                name: "quota_period_days",
                schema: "app",
                table: "user_subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quota_period_days",
                schema: "app",
                table: "subscription_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "plan_quota_period_days",
                schema: "app",
                table: "payment_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscription_plans_duration",
                schema: "app",
                table: "subscription_plans",
                sql: "(kind = 'free' AND duration_days IS NULL) OR (kind IN ('personal', 'family') AND duration_days >= 1 AND duration_days <= 3650)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscription_plans_quota_period",
                schema: "app",
                table: "subscription_plans",
                sql: "quota_period_days IS NULL OR (quota_period_days >= 1 AND (duration_days IS NULL OR quota_period_days <= duration_days))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_subscription_plans_duration",
                schema: "app",
                table: "subscription_plans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subscription_plans_quota_period",
                schema: "app",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "quota_period_days",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropColumn(
                name: "quota_period_days",
                schema: "app",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "plan_quota_period_days",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.AddColumn<int>(
                name: "estimated_questions",
                schema: "app",
                table: "subscription_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_subscription_plans_duration",
                schema: "app",
                table: "subscription_plans",
                sql: "kind = 'free' OR duration_days IN (30, 90)");
        }
    }
}
