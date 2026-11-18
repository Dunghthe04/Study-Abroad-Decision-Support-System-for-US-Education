namespace StudyAbroad.Application.Profile.Flow;

/// <summary>Tài khoản phụ huynh không có hồ sơ riêng (1.5d), không được lưu hồ sơ của chính mình. Controller trả 409.
/// Kế thừa ArgumentException để chat (chỉ bắt ArgumentException khi ghi nháp) bỏ qua êm, không hỏng lượt chat.</summary>
public sealed class ProfileLockedForParentException()
    : ArgumentException("Tài khoản phụ huynh không có hồ sơ riêng. Bạn nhập hồ sơ cho con ở mục Con của tôi.");
