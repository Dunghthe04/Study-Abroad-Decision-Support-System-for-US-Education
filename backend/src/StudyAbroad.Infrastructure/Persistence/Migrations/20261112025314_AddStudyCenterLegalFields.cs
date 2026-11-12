using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyCenterLegalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "certified_counselor_count",
                schema: "app",
                table: "study_centers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legal_compliance_mode",
                schema: "app",
                table: "study_centers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "licensed");

            migrationBuilder.AddColumn<List<string>>(
                name: "legal_document_urls",
                schema: "app",
                table: "study_centers",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "legal_representative",
                schema: "app",
                table: "study_centers",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "license_issued_by",
                schema: "app",
                table: "study_centers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "license_issued_date",
                schema: "app",
                table: "study_centers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_code",
                schema: "app",
                table: "study_centers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "training_license_number",
                schema: "app",
                table: "study_centers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verification_note",
                schema: "app",
                table: "study_centers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "certified_counselor_count",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "legal_compliance_mode",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "legal_document_urls",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "legal_representative",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "license_issued_by",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "license_issued_date",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "tax_code",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "training_license_number",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "verification_note",
                schema: "app",
                table: "study_centers");
        }
    }
}
