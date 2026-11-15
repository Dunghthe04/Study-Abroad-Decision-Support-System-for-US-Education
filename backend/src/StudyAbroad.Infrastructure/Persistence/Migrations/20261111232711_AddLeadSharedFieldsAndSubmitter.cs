using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadSharedFieldsAndSubmitter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_leads_user_id",
                schema: "app",
                table: "leads");

            migrationBuilder.AddColumn<bool>(
                name: "acknowledged_center_contact",
                schema: "app",
                table: "leads",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<List<string>>(
                name: "shared_fields",
                schema: "app",
                table: "leads",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<Guid>(
                name: "submitted_by_user_id",
                schema: "app",
                table: "leads",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_leads_submitted_by_user_id_created_at",
                schema: "app",
                table: "leads",
                columns: new[] { "submitted_by_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_user_id_created_at",
                schema: "app",
                table: "leads",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_leads_users_submitted_by_user_id",
                schema: "app",
                table: "leads",
                column: "submitted_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Lead cũ: học sinh tự gửi; đã chia sẻ hồ sơ thì coi như chia sẻ cả 5 nhóm, không thì chưa chọn nhóm nào.
            migrationBuilder.Sql("""
                UPDATE app.leads
                SET submitted_by_user_id = user_id,
                    shared_fields = CASE WHEN shared_profile
                        THEN ARRAY['contact','academic','tests','goals','budget']
                        ELSE '{}'::text[] END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_leads_users_submitted_by_user_id",
                schema: "app",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "ix_leads_submitted_by_user_id_created_at",
                schema: "app",
                table: "leads");

            migrationBuilder.DropIndex(
                name: "ix_leads_user_id_created_at",
                schema: "app",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "acknowledged_center_contact",
                schema: "app",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "shared_fields",
                schema: "app",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "submitted_by_user_id",
                schema: "app",
                table: "leads");

            migrationBuilder.CreateIndex(
                name: "ix_leads_user_id",
                schema: "app",
                table: "leads",
                column: "user_id");
        }
    }
}
