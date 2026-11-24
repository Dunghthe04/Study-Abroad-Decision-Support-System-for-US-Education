using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Auth
{
    public interface IUserRepository
    {
        Task<bool> EmailExistAsync(string email, CancellationToken cancellationToken = default);
        Task<bool> PhoneExistAsync(string phone, CancellationToken cancellationToken = default);
        Task AddAsync(User user, CancellationToken cancellationToken = default);
        Task<User?> GetUserByEmail(string email, CancellationToken cancellationToken = default);
        Task<User?> GetUserById(Guid id, CancellationToken cancellationToken = default);
        Task UpdateUserAsync(User user, CancellationToken cancellationToken = default);

        // [USAS-362] Quản lý phiên làm việc của người dùng
        Task CreateSessionAsync(UserSession session, CancellationToken cancellationToken = default);
        Task<UserSession?> GetSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
        Task RevokeSessionAsync(string tokenHash, CancellationToken cancellationToken = default);
        Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task RevokeOtherSessionsAsync(Guid userId, string keepTokenHash, CancellationToken cancellationToken = default);
        /// <summary>Phiên có refresh token hiện tại hoặc refresh token vừa bị đổi khớp hash này.</summary>
        Task<UserSession?> GetSessionByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default);
        /// <summary>Đổi refresh token và access token của phiên, chỉ khi refresh token hiện tại vẫn là oldRefreshHash và phiên chưa bị thu hồi.
        /// false = request khác đã đổi trước (hai tab gia hạn cùng lúc).</summary>
        Task<bool> RotateRefreshTokenAsync(Guid sessionId, string oldRefreshHash, string newRefreshHash, string newAccessHash, CancellationToken cancellationToken = default);
        Task RevokeSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
        /// <summary>1.5b: người dùng còn ít nhất một phiên chưa thu hồi và chưa hết hạn (phụ huynh đăng xuất hết thì phiên làm thay mất tác dụng).</summary>
        Task<bool> HasActiveSessionAsync(Guid userId, CancellationToken cancellationToken = default);

        // [USAS-12 / USAS-17] Quản lý mã OTP
        Task SaveOtpAsync(OtpToken token, CancellationToken cancellationToken = default);
        Task UpdateOtpAsync(OtpToken token, CancellationToken cancellationToken = default);
        Task<OtpToken?> GetLatestOtpAsync(Guid userId, string purpose, CancellationToken cancellationToken = default);
        Task InvalidateOtpsAsync(Guid userId, string purpose, CancellationToken cancellationToken = default);
        /// <summary>[UC-75 4b] Mã chưa dùng có hash và mục đích khớp; dùng cho link trong mail không kèm id người dùng.</summary>
        Task<OtpToken?> FindUnusedOtpByHashAsync(string codeHash, IReadOnlyCollection<string> purposes, CancellationToken cancellationToken = default);
        Task<int> CountOtpRequestsInLastHourAsync(Guid userId, string purpose, CancellationToken cancellationToken = default);

        // [USAS-362] Quản lý tác vụ chạy nền (Background Scheduled Jobs)
        Task<int> UnlockExpiredAccountsAsync(CancellationToken cancellationToken = default);
        Task<int> CleanupExpiredOtpsAsync(CancellationToken cancellationToken = default);

        // [USAS-362] Quản lý danh sách người dùng (dành cho Admin)
        Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default);

        /// <summary>[UC-60] Danh sách tài khoản có tìm, lọc, phân trang.</summary>
        Task<PagedResult<User>> GetAdminUserAccountListAsync(AdminUserAccountListQuery query, CancellationToken cancellationToken = default);
    }
}
