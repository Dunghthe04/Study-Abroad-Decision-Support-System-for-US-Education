using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniversityAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "aliases",
                schema: "app",
                table: "universities",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'",
                comment: "Tên viết tắt học sinh hay gõ (MIT, UT Austin, ASU). Một tên có thể nằm ở nhiều trường: chat hỏi lại để học sinh chọn.");

            // Tên viết tắt nhóm soạn ngày 27/11/2026 cho các trường hay được nhắc. Quy ước:
            // - Tên mơ hồ thật thì gán cho mọi trường dùng nó (ASU, OSU, MSU, Penn): chat hiện nút để học sinh chọn.
            // - Không dùng tên trùng chữ thường gặp: UK (nước Anh), UC (cả hệ University of California), LUC ("lực"), NEU ("nếu").
            // Chỉ ghi tên không suy ra được từ tên trường; tên bắt đầu đúng như tên trường (Purdue, Harvard) đã khớp sẵn.
            migrationBuilder.Sql("""
                UPDATE app.universities u
                SET aliases = v.aliases
                FROM (VALUES
                    ('166683', ARRAY['MIT']),
                    ('110404', ARRAY['Caltech']),
                    ('211440', ARRAY['CMU']),
                    ('193900', ARRAY['NYU']),
                    ('110662', ARRAY['UCLA']),
                    ('110635', ARRAY['UC Berkeley', 'UCB', 'Berkeley']),
                    ('110644', ARRAY['UC Davis', 'UCD']),
                    ('110653', ARRAY['UC Irvine', 'UCI']),
                    ('110671', ARRAY['UC Riverside', 'UCR']),
                    ('110680', ARRAY['UC San Diego', 'UCSD']),
                    ('110705', ARRAY['UC Santa Barbara', 'UCSB']),
                    ('110714', ARRAY['UC Santa Cruz', 'UCSC']),
                    ('123961', ARRAY['USC']),
                    ('215062', ARRAY['UPenn', 'Penn']),
                    ('214777', ARRAY['Penn State', 'PSU', 'Penn']),
                    ('228778', ARRAY['UT Austin']),
                    ('228787', ARRAY['UT Dallas', 'UTD']),
                    ('229027', ARRAY['UT San Antonio', 'UTSA']),
                    ('221759', ARRAY['UTK', 'UT Knoxville']),
                    ('170976', ARRAY['UMich']),
                    ('171100', ARRAY['MSU']),
                    ('176080', ARRAY['MSU']),
                    ('180461', ARRAY['MSU']),
                    ('204796', ARRAY['OSU']),
                    ('209542', ARRAY['OSU']),
                    ('207388', ARRAY['OSU']),
                    ('104151', ARRAY['ASU', 'Arizona State']),
                    ('197869', ARRAY['ASU', 'App State']),
                    ('134097', ARRAY['FSU']),
                    ('133951', ARRAY['FIU']),
                    ('133669', ARRAY['FAU']),
                    ('132903', ARRAY['UCF']),
                    ('137351', ARRAY['USF']),
                    ('136172', ARRAY['UNF']),
                    ('433660', ARRAY['FGCU']),
                    ('134130', ARRAY['UF']),
                    ('139755', ARRAY['Georgia Tech', 'GaTech']),
                    ('139940', ARRAY['GSU']),
                    ('139959', ARRAY['UGA']),
                    ('139861', ARRAY['GCSU']),
                    ('162928', ARRAY['JHU']),
                    ('131469', ARRAY['GWU', 'GW']),
                    ('232186', ARRAY['GMU']),
                    ('190150', ARRAY['Columbia', 'Columbia University']),
                    ('144050', ARRAY['UChicago']),
                    ('201645', ARRAY['CWRU']),
                    ('194824', ARRAY['RPI']),
                    ('195003', ARRAY['RIT']),
                    ('168421', ARRAY['WPI']),
                    ('185828', ARRAY['NJIT']),
                    ('152318', ARRAY['RHIT']),
                    ('239318', ARRAY['MSOE']),
                    ('233921', ARRAY['Virginia Tech', 'VT']),
                    ('234030', ARRAY['VCU']),
                    ('232423', ARRAY['JMU']),
                    ('231624', ARRAY['W&M']),
                    ('199193', ARRAY['NC State', 'NCSU']),
                    ('199139', ARRAY['UNC Charlotte', 'UNCC']),
                    ('199218', ARRAY['UNC Wilmington', 'UNCW']),
                    ('159391', ARRAY['LSU']),
                    ('228246', ARRAY['SMU']),
                    ('228875', ARRAY['TCU']),
                    ('229115', ARRAY['TTU']),
                    ('225511', ARRAY['UH']),
                    ('227216', ARRAY['UNT']),
                    ('174066', ARRAY['UMN']),
                    ('240444', ARRAY['UW-Madison']),
                    ('166629', ARRAY['UMass', 'UMass Amherst']),
                    ('163286', ARRAY['UMD', 'UMCP']),
                    ('163268', ARRAY['UMBC']),
                    ('129020', ARRAY['UConn']),
                    ('217484', ARRAY['URI']),
                    ('231174', ARRAY['UVM']),
                    ('161253', ARRAY['UMaine']),
                    ('183044', ARRAY['UNH']),
                    ('215293', ARRAY['Pitt']),
                    ('151351', ARRAY['IU', 'IU Bloomington']),
                    ('151111', ARRAY['IUI', 'IUPUI']),
                    ('181464', ARRAY['UNL']),
                    ('182281', ARRAY['UNLV']),
                    ('182290', ARRAY['UNR']),
                    ('135726', ARRAY['UMiami']),
                    ('152080', ARRAY['Notre Dame']),
                    ('110422', ARRAY['Cal Poly', 'Cal Poly SLO']),
                    ('110529', ARRAY['Cal Poly Pomona', 'CPP']),
                    ('110565', ARRAY['Cal State Fullerton', 'CSUF']),
                    ('110583', ARRAY['Cal State Long Beach', 'Long Beach State', 'CSULB']),
                    ('110592', ARRAY['Cal State LA', 'CSULA']),
                    ('122755', ARRAY['SJSU']),
                    ('117946', ARRAY['LMU']),
                    ('146719', ARRAY['Loyola Chicago']),
                    ('100663', ARRAY['UAB']),
                    ('196088', ARRAY['UB', 'SUNY Buffalo']),
                    ('196097', ARRAY['SBU', 'SUNY Stony Brook']),
                    ('196079', ARRAY['SUNY Binghamton']),
                    ('218663', ARRAY['UofSC']),
                    ('221999', ARRAY['Vandy']),
                    ('238032', ARRAY['WVU'])
                ) AS v(code, aliases)
                WHERE u.code = v.code;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aliases",
                schema: "app",
                table: "universities");
        }
    }
}
