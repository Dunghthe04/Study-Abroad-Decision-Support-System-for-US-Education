using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadParentConfirm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "parent_decided_at",
                schema: "app",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "parent_decision_ip",
                schema: "app",
                table: "leads",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "parent_email",
                schema: "app",
                table: "leads",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "parent_token_expires_at",
                schema: "app",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "parent_token_hash",
                schema: "app",
                table: "leads",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_leads_parent_token_hash",
                schema: "app",
                table: "leads",
                column: "parent_token_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_leads_parent_token_hash",
                schema: "app",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "parent_decided_at",
                schema: "app",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "parent_decision_ip",
                schema: "app",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "parent_email",
                schema: "app",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "parent_token_expires_at",
                schema: "app",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "parent_token_hash",
                schema: "app",
                table: "leads");
        }
    }
}
