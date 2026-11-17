using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCenterReviewUniqueLead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_center_reviews_lead_id",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.CreateIndex(
                name: "ix_center_reviews_lead_id",
                schema: "app",
                table: "center_reviews",
                column: "lead_id",
                unique: true,
                filter: "lead_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_center_reviews_lead_id",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.CreateIndex(
                name: "ix_center_reviews_lead_id",
                schema: "app",
                table: "center_reviews",
                column: "lead_id");
        }
    }
}
