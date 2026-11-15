using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadMessageReportAndCenterSuspensionProposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "report_reason",
                schema: "app",
                table: "lead_messages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reported_at",
                schema: "app",
                table: "lead_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reported_by_user_id",
                schema: "app",
                table: "lead_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "center_suspension_proposals",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "pending"),
                    removed_message_count = table.Column<int>(type: "integer", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_center_suspension_proposals", x => x.id);
                    table.ForeignKey(
                        name: "fk_center_suspension_proposals_study_centers_study_center_id",
                        column: x => x.study_center_id,
                        principalSchema: "app",
                        principalTable: "study_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_center_suspension_proposals_users_decided_by_user_id",
                        column: x => x.decided_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_center_suspension_proposals_users_proposed_by_user_id",
                        column: x => x.proposed_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lead_messages_reported_by_user_id",
                schema: "app",
                table: "lead_messages",
                column: "reported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_suspension_proposals_decided_by_user_id",
                schema: "app",
                table: "center_suspension_proposals",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_suspension_proposals_proposed_by_user_id",
                schema: "app",
                table: "center_suspension_proposals",
                column: "proposed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_suspension_proposals_status_created_at",
                schema: "app",
                table: "center_suspension_proposals",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_center_suspension_proposals_study_center_id",
                schema: "app",
                table: "center_suspension_proposals",
                column: "study_center_id",
                unique: true,
                filter: "status = 'pending'");

            migrationBuilder.AddForeignKey(
                name: "fk_lead_messages_users_reported_by_user_id",
                schema: "app",
                table: "lead_messages",
                column: "reported_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_lead_messages_users_reported_by_user_id",
                schema: "app",
                table: "lead_messages");

            migrationBuilder.DropTable(
                name: "center_suspension_proposals",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_lead_messages_reported_by_user_id",
                schema: "app",
                table: "lead_messages");

            migrationBuilder.DropColumn(
                name: "report_reason",
                schema: "app",
                table: "lead_messages");

            migrationBuilder.DropColumn(
                name: "reported_at",
                schema: "app",
                table: "lead_messages");

            migrationBuilder.DropColumn(
                name: "reported_by_user_id",
                schema: "app",
                table: "lead_messages");
        }
    }
}
