using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCenterVerificationRequestAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "verification_status",
                schema: "app",
                table: "study_centers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "unverified",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldDefaultValue: "unverified");

            migrationBuilder.AddColumn<DateTime>(
                name: "commitment_agreed_at",
                schema: "app",
                table: "study_centers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "email_verified_at",
                schema: "app",
                table: "study_centers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fee_description",
                schema: "app",
                table: "study_centers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fee_max",
                schema: "app",
                table: "study_centers",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fee_min",
                schema: "app",
                table: "study_centers",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "phone_verified_at",
                schema: "app",
                table: "study_centers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registrant_name",
                schema: "app",
                table: "study_centers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registrant_position",
                schema: "app",
                table: "study_centers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "terms_agreed_at",
                schema: "app",
                table: "study_centers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "center_verification_requests",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    tax_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    legal_representative = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    website = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "pending_review"),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    review_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_national_registry_matched = table.Column<bool>(type: "boolean", nullable: false),
                    is_business_operating = table.Column<bool>(type: "boolean", nullable: false),
                    is_representative_valid = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_center_verification_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_center_verification_requests_study_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "app",
                        principalTable: "study_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_center_verification_requests_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_center_verification_requests_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "center_verification_documents",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    file_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_center_verification_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_center_verification_documents_center_verification_requests_",
                        column: x => x.request_id,
                        principalSchema: "app",
                        principalTable: "center_verification_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_center_verification_documents_study_centers_center_id",
                        column: x => x.center_id,
                        principalSchema: "app",
                        principalTable: "study_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_center_verification_documents_center_id",
                schema: "app",
                table: "center_verification_documents",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_verification_documents_request_id",
                schema: "app",
                table: "center_verification_documents",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_verification_requests_center_id",
                schema: "app",
                table: "center_verification_requests",
                column: "center_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_verification_requests_requested_by_user_id",
                schema: "app",
                table: "center_verification_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_verification_requests_reviewed_by_user_id",
                schema: "app",
                table: "center_verification_requests",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_verification_requests_status",
                schema: "app",
                table: "center_verification_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_center_verification_requests_tax_code",
                schema: "app",
                table: "center_verification_requests",
                column: "tax_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "center_verification_documents",
                schema: "app");

            migrationBuilder.DropTable(
                name: "center_verification_requests",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "commitment_agreed_at",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "email_verified_at",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "fee_description",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "fee_max",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "fee_min",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "phone_verified_at",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "registrant_name",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "registrant_position",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "terms_agreed_at",
                schema: "app",
                table: "study_centers");

            migrationBuilder.AlterColumn<string>(
                name: "verification_status",
                schema: "app",
                table: "study_centers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "unverified",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "unverified");
        }
    }
}
