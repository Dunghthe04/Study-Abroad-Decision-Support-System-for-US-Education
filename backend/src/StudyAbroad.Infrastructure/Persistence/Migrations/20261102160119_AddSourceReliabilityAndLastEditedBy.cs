using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceReliabilityAndLastEditedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "last_edited_by_user_id",
                schema: "app",
                table: "university_offerings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_reliability",
                schema: "app",
                table: "university_offerings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_edited_by_user_id",
                schema: "app",
                table: "universities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_university_offerings_last_edited_by_user_id",
                schema: "app",
                table: "university_offerings",
                column: "last_edited_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_university_offerings_source_reliability",
                schema: "app",
                table: "university_offerings",
                sql: "source_reliability IS NULL OR source_reliability IN ('official', 'aggregated', 'self_reported')");

            migrationBuilder.CreateIndex(
                name: "ix_universities_last_edited_by_user_id",
                schema: "app",
                table: "universities",
                column: "last_edited_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_universities_users_last_edited_by_user_id",
                schema: "app",
                table: "universities",
                column: "last_edited_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_university_offerings_users_last_edited_by_user_id",
                schema: "app",
                table: "university_offerings",
                column: "last_edited_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(ReadSqlFile("20261102160119_AddSourceReliabilityAndLastEditedBy.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_universities_users_last_edited_by_user_id",
                schema: "app",
                table: "universities");

            migrationBuilder.DropForeignKey(
                name: "fk_university_offerings_users_last_edited_by_user_id",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropIndex(
                name: "ix_university_offerings_last_edited_by_user_id",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_university_offerings_source_reliability",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropIndex(
                name: "ix_universities_last_edited_by_user_id",
                schema: "app",
                table: "universities");

            migrationBuilder.DropColumn(
                name: "last_edited_by_user_id",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "source_reliability",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "last_edited_by_user_id",
                schema: "app",
                table: "universities");
        }

        private static string ReadSqlFile(string fileName)
        {
            var assembly = typeof(AddSourceReliabilityAndLastEditedBy).Assembly;
            var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith("." + fileName));
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
