using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillUniversityAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bổ sung tên viết tắt cho các trường còn trống sau AddUniversityAliases (sinh bằng scripts/gen_university_aliases_20261127.py).
            // Soạn tay, không suy chữ cái đầu: chat tự chọn trường khi khớp alias nên chỉ ghi tên viết tắt thật.
            // Không dùng chữ trùng từ thường gặp: USA, USD, UK, UP, DU ("du học"), UA ("ủa").
            // Trường có tên đủ đặc trưng (Harvard, Stanford, Rice) không cần alias: gõ tên là khớp.
            // Chỉ ghi vào trường còn trống để không đè alias Moderator đã sửa.
            migrationBuilder.Sql("""
                UPDATE app.universities u
                SET aliases = v.aliases
                FROM (VALUES
                    ('164988', ARRAY['BU', 'Boston U']),
                    ('112260', ARRAY['CMC']),
                    ('126818', ARRAY['CSU Fort Collins']),
                    ('218724', ARRAY['CCU']),
                    ('128902', ARRAY['Conn College']),
                    ('212577', ARRAY['F&M', 'Franklin & Marshall']),
                    ('115409', ARRAY['HMC', 'Mudd']),
                    ('191630', ARRAY['HWS']),
                    ('145646', ARRAY['IWU']),
                    ('153603', ARRAY['ISU']),
                    ('155399', ARRAY['K-State', 'KSU']),
                    ('209056', ARRAY['Lewis and Clark']),
                    ('171128', ARRAY['Michigan Tech', 'MTU']),
                    ('166939', ARRAY['MHC']),
                    ('262129', ARRAY['NCF']),
                    ('200332', ARRAY['NDSU']),
                    ('105330', ARRAY['NAU']),
                    ('204909', ARRAY['OWU']),
                    ('122931', ARRAY['SCU']),
                    ('195304', ARRAY['SLC']),
                    ('196167', ARRAY['SUNY Geneseo', 'Geneseo']),
                    ('196103', ARRAY['SUNY ESF', 'ESF']),
                    ('196185', ARRAY['Oneonta']),
                    ('195216', ARRAY['Saint Lawrence']),
                    ('163912', ARRAY['SMCM']),
                    ('174844', ARRAY['Saint Olaf']),
                    ('186867', ARRAY['Stevens Tech']),
                    ('196413', ARRAY['Cuse']),
                    ('228459', ARRAY['TXST']),
                    ('187134', ARRAY['TCNJ']),
                    ('206589', ARRAY['Wooster']),
                    ('100751', ARRAY['Bama']),
                    ('104179', ARRAY['UArizona', 'U of A']),
                    ('106397', ARRAY['UArk', 'U of A']),
                    ('202480', ARRAY['UDayton', 'UD']),
                    ('153658', ARRAY['UIowa']),
                    ('155317', ARRAY['KU']),
                    ('157085', ARRAY['UKY']),
                    ('232681', ARRAY['UMW']),
                    ('176017', ARRAY['Ole Miss']),
                    ('187985', ARRAY['UNM']),
                    ('207500', ARRAY['OU']),
                    ('209551', ARRAY['UO', 'UOregon']),
                    ('209825', ARRAY['UPortland']),
                    ('233374', ARRAY['UR']),
                    ('195030', ARRAY['UR', 'U of R']),
                    ('102094', ARRAY['South Alabama']),
                    ('230764', ARRAY['UofU', 'U of U']),
                    ('240727', ARRAY['UWyo']),
                    ('216597', ARRAY['Nova']),
                    ('199847', ARRAY['WFU']),
                    ('234207', ARRAY['W&L', 'WLU']),
                    ('230959', ARRAY['Midd']),
                    ('150136', ARRAY['BSU']),
                    ('192703', ARRAY['Manhattan College']),
                    ('209065', ARRAY['Linfield College'])
                ) AS v(code, aliases)
                WHERE u.code = v.code AND cardinality(u.aliases) = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE app.universities SET aliases = '{}' WHERE code IN ('164988', '112260', '126818', '218724', '128902', '212577', '115409', '191630', '145646', '153603', '155399', '209056', '171128', '166939', '262129', '200332', '105330', '204909', '122931', '195304', '196167', '196103', '196185', '195216', '163912', '174844', '186867', '196413', '228459', '187134', '206589', '100751', '104179', '106397', '202480', '153658', '155317', '157085', '232681', '176017', '187985', '207500', '209551', '209825', '233374', '195030', '102094', '230764', '240727', '216597', '199847', '234207', '230959', '150136', '192703', '209065');");
        }
    }
}
