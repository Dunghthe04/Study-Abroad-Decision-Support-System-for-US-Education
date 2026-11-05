using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Auth;

/// <summary>
/// [USAS-362] Dịch vụ tạo JWT Token và băm Token để lưu phiên.
/// </summary>
public interface ITokenService
{
    /// <summary>Sinh chuỗi JWT Token và trả về thời điểm hết hạn.</summary>
    (string Token, DateTime ExpiresAt) GenerateToken(User user);

    /// <summary>Băm chuỗi JWT bằng SHA-256 để lưu vào bảng user_sessions (tìm kiếm nhanh, an toàn).</summary>
    string HashToken(string token);
}
