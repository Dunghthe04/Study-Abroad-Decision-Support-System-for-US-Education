using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyAbroad.Infrastructure.Persistence;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>Bỏ hai bảng không code nào đọc (state_living_costs, data_import_batches) và cột ranking_lists.rank_type.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261119123000_DropUnusedTablesAndRankType")]
    public partial class DropUnusedTablesAndRankType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF EXISTS: DB dựng từ database/init có thể đã có hoặc chưa có hai bảng này
            migrationBuilder.Sql("DROP TABLE IF EXISTS app.state_living_costs;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS app.data_import_batches;");
            migrationBuilder.Sql("ALTER TABLE app.ranking_lists DROP COLUMN IF EXISTS rank_type;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE app.ranking_lists ADD COLUMN IF NOT EXISTS rank_type varchar(32);");
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS app.state_living_costs (
                    state_code varchar(8) PRIMARY KEY, state_name varchar(64) NOT NULL,
                    cost_index numeric(6,2) NOT NULL DEFAULT 100.00, avg_living_usd_yearly numeric(12,2),
                    notes varchar(500), source_url varchar(500), retrieved_at date,
                    created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz);
                CREATE TABLE IF NOT EXISTS app.data_import_batches (
                    id uuid PRIMARY KEY, batch_code varchar(64) NOT NULL UNIQUE, source_name varchar(128),
                    data_year varchar(32), total_rows int NOT NULL DEFAULT 0, passed_rows int NOT NULL DEFAULT 0,
                    flagged_rows int NOT NULL DEFAULT 0, checked_by varchar(128) NOT NULL DEFAULT 'USAS Research Team',
                    status varchar(32) NOT NULL DEFAULT 'verified', created_at timestamptz NOT NULL DEFAULT now());
                """);
        }
    }
}
