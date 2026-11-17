namespace StudyAbroad.Application.Auth
{
    /// <summary>
    /// [UC-91] Kênh gửi mã OTP qua SMS để xác thực số điện thoại trung tâm. Cài đặt thật (nhà cung cấp SMS) thay vào qua DI.
    /// </summary>
    public interface ISmsSender
    {
        /// <summary>
        /// True khi tin nhắn thật sự tới được người nhận (đã cấu hình nhà cung cấp SMS, hoặc đang ở môi trường phát triển nơi mã
        /// được ghi ra log). False thì luồng xác thực số điện thoại báo "chưa sẵn sàng" và quy tắc bắt buộc xác thực số điện thoại
        /// trước khi nộp hồ sơ xác minh không được áp dụng, để trung tâm không bị kẹt.
        /// </summary>
        bool CanDeliver { get; }

        Task SendOtpAsync(string phoneNumber, string otpCode, int expiryMinutes, CancellationToken ct = default);
    }
}
