using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

/// <summary>
/// [UC-110 / UC-150] Giữ yêu cầu liên hệ khi Admin duyệt ngày sinh làm học sinh về dưới 16:
/// yêu cầu chưa kết thúc chuyển "pending_parent", nhớ trạng thái cũ (<see cref="Lead.HeldFromStatus"/>) và lúc giữ (<see cref="Lead.HeldAt"/>).
/// Chạy chung transaction của bên gọi.
/// </summary>
public interface ILeadHoldRepository
{
    /// <summary>
    /// Lead của học sinh đang "new", "accepted" hoặc "contacted" chuyển "pending_parent", hạn phụ huynh trả lời là <paramref name="expiresAt"/>.
    /// Chưa có token: mail UC-110 chỉ gửi sau khi phụ huynh đồng ý tài khoản. Trả số lead đã giữ.
    /// </summary>
    Task<int> HoldOpenLeadsAsync(Guid userId, DateTime now, DateTime expiresAt, CancellationToken ct = default);

    /// <summary>Lead đang giữ của học sinh (còn "pending_parent" và có <see cref="Lead.HeldFromStatus"/>).</summary>
    Task<IReadOnlyList<Lead>> ListHeldAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Ghi hash token mail UC-110 cho lead còn đang giữ. False khi lead đã không còn chờ phụ huynh.</summary>
    Task<bool> SetParentTokenHashAsync(Guid leadId, string tokenHash, DateTime now, CancellationToken ct = default);

    /// <summary>Lead đang giữ của học sinh chuyển "withdrawn" (giữ <see cref="Lead.HeldFromStatus"/> làm dấu trung tâm đã từng nhận), xóa hash token.</summary>
    Task<int> WithdrawHeldAsync(Guid userId, DateTime now, CancellationToken ct = default);
}
