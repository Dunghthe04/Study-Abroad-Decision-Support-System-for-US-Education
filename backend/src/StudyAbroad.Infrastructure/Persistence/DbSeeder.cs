using Microsoft.EntityFrameworkCore;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Auth;

namespace StudyAbroad.Infrastructure.Persistence;

/// <summary>Seeds a few demo rows so the UI has data in development. Real data is imported from the survey.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        // 1. Seed trung tâm du học mẫu
        if (!await db.StudyCenters.AnyAsync(ct))
        {
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

        // 2. [USAS-362] Seed tài khoản Quản trị viên (Admin) mặc định
        if (!await db.Users.AnyAsync(u => u.Role == UserRoles.Admin, ct))
        {
            var hasher = new PasswordHasher();
            db.Users.Add(new User
            {
                Email = "admin@usas.edu.vn",
                PasswordHash = hasher.Hash("123456789"),
                FullName = "Quản trị viên Hệ thống",
                Role = UserRoles.Admin,
                Status = UserStatuses.Active,
                Phone = "0988888888"
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
