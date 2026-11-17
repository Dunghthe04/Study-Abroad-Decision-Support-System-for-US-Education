namespace StudyAbroad.Application.Leads;

/// <summary>[UC-80] Thu hồi các yêu cầu liên hệ còn mở khi người gửi xóa tài khoản.</summary>
public interface ILeadWithdrawalRepository
{
    /// <summary>
    /// Chuyển các lead của người dùng (leads.user_id) đang "new" hoặc "contacted" sang "withdrawn".
    /// Lead "closed" giữ nguyên. Trả về số lead đã thu hồi.
    /// </summary>
    Task<int> WithdrawOpenLeadsAsync(Guid userId, DateTime now, CancellationToken ct = default);
}
