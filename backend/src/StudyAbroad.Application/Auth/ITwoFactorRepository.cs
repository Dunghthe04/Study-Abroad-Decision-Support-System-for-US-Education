using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Auth
{
    /// <summary>
    /// [UC-03 3d] Truy vấn riêng cho xác thực hai lớp mà IUserRepository chưa có. Tách interface để không phải
    /// sửa mọi lớp giả của IUserRepository trong test của người khác. Mã khôi phục nằm ở bảng otp_tokens (purpose recovery_code).
    /// </summary>
    public interface ITwoFactorRepository
    {
        /// <summary>Số mã khôi phục chưa dùng của tài khoản.</summary>
        Task<int> CountUnusedRecoveryCodesAsync(Guid userId, CancellationToken ct = default);
    }
}
