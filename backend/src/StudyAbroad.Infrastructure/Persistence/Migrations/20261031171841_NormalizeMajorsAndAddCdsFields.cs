using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeMajorsAndAddCdsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "majors",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "stem_majors",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.AddColumn<int>(
                name: "act25",
                schema: "app",
                table: "university_offerings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "act75",
                schema: "app",
                table: "university_offerings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "admitted_total",
                schema: "app",
                table: "university_offerings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "applicants_total",
                schema: "app",
                table: "university_offerings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gpa_submit_pct",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "intl_admitted",
                schema: "app",
                table: "university_offerings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "intl_aid_recipients",
                schema: "app",
                table: "university_offerings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "intl_applicants",
                schema: "app",
                table: "university_offerings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_ivy",
                schema: "app",
                table: "universities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "majors",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    cip_code = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_majors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "university_offering_majors",
                schema: "app",
                columns: table => new
                {
                    university_offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    major_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_stem = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_university_offering_majors", x => new { x.university_offering_id, x.major_id });
                    table.ForeignKey(
                        name: "fk_university_offering_majors_majors_major_id",
                        column: x => x.major_id,
                        principalSchema: "app",
                        principalTable: "majors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_university_offering_majors_university_offerings_university_",
                        column: x => x.university_offering_id,
                        principalSchema: "app",
                        principalTable: "university_offerings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_university_offerings_act_range",
                schema: "app",
                table: "university_offerings",
                sql: "(act25 IS NULL OR act25 BETWEEN 1 AND 36) AND (act75 IS NULL OR act75 BETWEEN 1 AND 36) AND (act25 IS NULL OR act75 IS NULL OR act25 <= act75)");

            migrationBuilder.CreateIndex(
                name: "ix_majors_cip_code",
                schema: "app",
                table: "majors",
                column: "cip_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_majors_name",
                schema: "app",
                table: "majors",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_university_offering_majors_major_id",
                schema: "app",
                table: "university_offering_majors",
                column: "major_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "university_offering_majors",
                schema: "app");

            migrationBuilder.DropTable(
                name: "majors",
                schema: "app");

            migrationBuilder.DropCheckConstraint(
                name: "ck_university_offerings_act_range",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "act25",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "act75",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "admitted_total",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "applicants_total",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "gpa_submit_pct",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "intl_admitted",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "intl_aid_recipients",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "intl_applicants",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "is_ivy",
                schema: "app",
                table: "universities");

            migrationBuilder.AddColumn<List<string>>(
                name: "majors",
                schema: "app",
                table: "university_offerings",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<List<string>>(
                name: "stem_majors",
                schema: "app",
                table: "university_offerings",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");
        }
    }
}
