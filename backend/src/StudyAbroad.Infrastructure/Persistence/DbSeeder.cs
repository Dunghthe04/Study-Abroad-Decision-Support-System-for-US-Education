using Microsoft.EntityFrameworkCore;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence;

/// <summary>Seeds a few demo rows so the UI has data in development. Real data is imported from the survey.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.StudyCenters.AnyAsync(ct)) return;

        db.StudyCenters.AddRange(
            new StudyCenter
            {
                Code = "DEMO_001", Name = "Demo Center Hà Nội", City = "Hà Nội",
                StudyLevels = [StudyLevels.Secondary, StudyLevels.Undergraduate, StudyLevels.Master],
                Services = ["CHON_TRUONG", "SAN_HOC_BONG", "LUYEN_PHONG_VAN"],
            },
            new StudyCenter
            {
                Code = "DEMO_002", Name = "Demo Center TP.HCM", City = "TP.HCM",
                StudyLevels = [StudyLevels.CommunityCollege, StudyLevels.Undergraduate, StudyLevels.Master, StudyLevels.Phd],
                Services = ["CHON_TRUONG", "LUYEN_THI", "SUA_BAI_LUAN"],
            });

        await db.SaveChangesAsync(ct);
    }
}
