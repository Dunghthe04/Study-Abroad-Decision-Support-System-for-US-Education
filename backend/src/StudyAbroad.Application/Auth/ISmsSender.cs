namespace StudyAbroad.Application.Auth;

/// <summary>
/// [UC-91] Gửi mã OTP tới số điện thoại qua SMS.
/// TODO(UC-91): chưa có nhà cung cấp SMS thật (eSMS, SpeedSMS, Twilio...). Hiện chỉ có bản ghi log cho môi trường phát triển.
/// Khi có nhà cung cấp, viết một lớp cài đặt mới và đăng ký thay cho LoggingSmsSender; không đổi nghiệp vụ OTP.
/// </summary>
public interface ISmsSender
{
    /// <summary>Gửi OTP. Ném <see cref="InvalidOperationException"/> nếu kênh SMS chưa sẵn sàng.</summary>
    Task SendOtpAsync(string phone, string code, int expiryMinutes, CancellationToken ct = default);
}
