using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Auth;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-03 3d] Truy vấn mã khôi phục xác thực hai lớp (bảng otp_tokens, purpose recovery_code).</summary>
public class TwoFactorRepository(AppDbContext db) : ITwoFactorRepository
{
    public Task<int> CountUnusedRecoveryCodesAsync(Guid userId, CancellationToken ct = default) =>
        db.OtpTokens.CountAsync(o => o.UserId == userId && o.Purpose == OtpPurposes.RecoveryCode && o.UsedAt == null, ct);
}
