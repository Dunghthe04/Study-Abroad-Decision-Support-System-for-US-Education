using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StackConcurrentPlanCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_user_subscriptions_one_active_per_lane",
                schema: "app",
                table: "user_subscriptions");

            migrationBuilder.DropIndex(
                name: "ux_user_subscriptions_one_queued_per_lane",
                schema: "app",
                table: "user_subscriptions");

            // Gói cộng dồn: kỳ queued cũ bắt đầu ngay từ lúc áp dụng migration (người mua đã trả tiền, không mất ngày đã mua)
            migrationBuilder.Sql(
                "UPDATE app.user_subscriptions SET status = 'active', starts_at = now(), ends_at = now() + (duration_days || ' days')::interval, updated_at = now() " +
                "WHERE status = 'queued';");

            // Chỗ ngồi gói Gia đình cố định 4 (2 Học sinh, 2 Phụ huynh): sửa mô tả gói đã seed
            migrationBuilder.Sql(
                "UPDATE app.subscription_plans SET description = replace(description, 'tối đa 3 tài khoản', 'tối đa 4 tài khoản (2 Học sinh, 2 Phụ huynh)') " +
                "WHERE kind = 'family' AND description LIKE '%tối đa 3 tài khoản%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dữ liệu không quay lại được: kỳ queued đã chuyển thành active; nếu một chủ có nhiều kỳ active cùng loại thì tạo lại index duy nhất sẽ lỗi
            // và phải kết thúc bớt kỳ trước.
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
        }
    }
}
