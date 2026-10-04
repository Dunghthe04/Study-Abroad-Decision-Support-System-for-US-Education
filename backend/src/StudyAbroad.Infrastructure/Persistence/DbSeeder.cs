using Microsoft.EntityFrameworkCore;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence;

/// <summary>Seeds a few demo rows so the UI has data in development. Real data is imported from the survey.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        await SeedStudyCentersAsync(db, ct);
        await SeedRecommendationSettingAsync(db, ct);
        await SeedDemoUniversitiesAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }
    private static async Task SeedStudyCentersAsync(AppDbContext db, CancellationToken ct)
    {
        if(await db.StudyCenters.AnyAsync(ct)) return;

        db.StudyCenters.AddRange(
           new StudyCenter
           {
               Code = "DEMO_001",
               Name = "Demo Center Hà Nội",
               City = "Hà Nội",
               StudyLevels = [StudyLevels.Secondary, StudyLevels.Undergraduate, StudyLevels.Master],
               Services = ["CHON_TRUONG", "SAN_HOC_BONG", "LUYEN_PHONG_VAN"],
           },
            new StudyCenter
            {
                Code = "DEMO_002",
                Name = "Demo Center TP.HCM",
                City = "TP.HCM",
                StudyLevels = [StudyLevels.CommunityCollege, StudyLevels.Undergraduate, StudyLevels.Master, StudyLevels.Phd],
                Services = ["CHON_TRUONG", "LUYEN_THI", "SUA_BAI_LUAN"],
            });
    }
    /// <summary>Ngưỡng và trọng số cho gợi ý trường (#6). Admin sửa ở đây, không cần sửa code.</summary>
    private static async Task SeedRecommendationSettingAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.AppSettings.AnyAsync(s => s.Key == "recommend.weights")) return;

        db.AppSettings.Add(new AppSetting
        {
            Key = "recommend.weights",
            Description = "Ngưỡng và trọng số cho gợi ý trường (#6)",
            ValueJson = """
            {
              "gpaBand": 0.3,
              "budgetTolerance": 0.10,
              "extracurricularWeight": 0.5,
              "financeWeight": 1.0,
              "maxResults": 12,
              "perCategory": { "reach": 3, "match": 5, "safety": 4 }
            }
            """,
        });
    }
    /// <summary>
    /// Trường DEMO với số liệu tự đặt (không phải trường thật) để test gợi ý trường.
    /// Mỗi trường cố ý rơi vào một tình huống: Reach, Match, Safety, thiếu GPA, thiếu SAT, thiếu cả hai, thiếu chi phí, vượt ngân sách, khác ngành.
    /// </summary>
    private static async Task SeedDemoUniversitiesAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Universities.AnyAsync(u => u.Code.StartsWith("DEMO_U"), ct)) return;

        string[] csBiz = ["Computer Science", "Business"];

        // (mã, tên, bang, ngành, avgGpa4, sat25, sat75, học phí, sinh hoạt, phí khác)
        var rows = new (string Code, string Name, string State, string[] Majors,
                        decimal? AvgGpa4, int? Sat25, int? Sat75,
                        decimal? Tuition, decimal? Living, decimal? Fees)[]
        {
            ("DEMO_UA", "Demo University A", "CA", csBiz,          3.90m, 1450, 1560, 60000m, 18000m, 2000m), // khó → Reach
            ("DEMO_UB", "Demo University B", "NY", csBiz,          3.50m, 1250, 1400, 35000m, 14000m, 1000m), // vừa → Match
            ("DEMO_UC", "Demo University C", "TX", csBiz,          3.10m, 1050, 1200, 22000m, 12000m, 1000m), // dễ → Safety
            ("DEMO_UD", "Demo University D", "WA", csBiz,          null,  1200, 1350, 26000m, 13000m, 1000m), // chỉ có SAT
            ("DEMO_UE", "Demo University E", "FL", csBiz,          3.40m, null, null, 30000m, 14000m, 1000m), // chỉ có GPA
            ("DEMO_UF", "Demo University F", "OH", csBiz,          null,  null, null, 18000m, 11000m, 1000m), // chưa đủ dữ liệu
            ("DEMO_UG", "Demo University G", "IL", csBiz,          3.30m, 1150, 1300, null,   null,   null),  // thiếu chi phí
            ("DEMO_UH", "Demo University H", "MA", csBiz,          3.60m, 1300, 1450, 95000m, 22000m, 3000m), // vượt ngân sách
            ("DEMO_UI", "Demo University I", "PA", ["Nursing"],    3.40m, 1200, 1350, 28000m, 13000m, 1000m), // khác ngành → bị lọc
        };

        foreach (var r in rows)
        {
            var uni = new University
            {
                Code = r.Code,
                Name = r.Name,
                State = r.State,
                Control = "private",
                IsActive = true,
            };
            db.Universities.Add(uni);
            db.UniversityOfferings.Add(new UniversityOffering
            {
                UniversityId = uni.Id,          // Id được sinh sẵn khi new (BaseEntity), dùng luôn được
                StudyLevel = StudyLevels.Undergraduate,
                Majors = [.. r.Majors],
                AcademicYear = "2026-27",
                AvgGpa4 = r.AvgGpa4,
                Sat25 = r.Sat25,
                Sat75 = r.Sat75,
                TuitionUsd = r.Tuition,
                LivingUsd = r.Living,
                FeesUsd = r.Fees,
                SatPolicy = "optional",
                Notes = "Dữ liệu DEMO để test, không phải số liệu thật.",
            });
        }

    }
}
