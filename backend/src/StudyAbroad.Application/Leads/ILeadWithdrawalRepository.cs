namespace StudyAbroad.Application.Leads;

/// <summary>[UC-80] Thu hồi các yêu cầu liên hệ còn mở khi người gửi xóa tài khoản; [UC-111] khi phụ huynh rút đồng ý.</summary>
public interface ILeadWithdrawalRepository
{
    /// <summary>
    /// Chuyển các lead của người dùng (leads.user_id) đang "new", "accepted" hoặc "contacted" sang "withdrawn".
    /// Lead "closed" giữ nguyên. Trả về số lead đã thu hồi.
    /// </summary>
    Task<int> WithdrawOpenLeadsAsync(Guid userId, DateTime now, CancellationToken ct = default);

    /// <summary>
    /// Chủ tài khoản trung tâm của các lead trung tâm đang thấy ("new", "accepted", "contacted"), mỗi người một lần.
    /// Gọi trước <see cref="WithdrawOpenLeadsAsync"/> để báo trung tâm yêu cầu đã bị thu hồi.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListCenterOwnersOfOpenLeadsAsync(Guid userId, CancellationToken ct = default);
}
