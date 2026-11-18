namespace StudyAbroad.Application.Profile.Flow;

/// <summary>Phụ huynh có liên kết còn hiệu lực không được lưu hồ sơ của chính mình. Controller trả 409.
/// Kế thừa ArgumentException để chat (chỉ bắt ArgumentException khi ghi nháp) bỏ qua êm, không hỏng lượt chat.</summary>
public sealed class ProfileLockedForParentException()
    : ArgumentException("Bạn đã liên kết với con. Hồ sơ do con nhập, bạn xem ở mục Con của tôi.");
