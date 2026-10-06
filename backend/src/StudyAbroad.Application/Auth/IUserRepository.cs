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

        // [USAS-12 / USAS-17] Quản lý mã OTP
        Task SaveOtpAsync(OtpToken token, CancellationToken cancellationToken = default);
        Task<OtpToken?> GetLatestOtpAsync(Guid userId, string purpose, CancellationToken cancellationToken = default);
        Task InvalidateOtpsAsync(Guid userId, string purpose, CancellationToken cancellationToken = default);

        // [USAS-362] Quản lý danh sách người dùng (dành cho Admin)
        Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    }
}
