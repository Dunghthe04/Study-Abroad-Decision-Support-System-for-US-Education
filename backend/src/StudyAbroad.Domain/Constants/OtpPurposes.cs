namespace StudyAbroad.Domain.Constants
{
    public static class OtpPurposes
    {
        /// <summary>Xác thực địa chỉ email khi đăng ký tài khoản mới.</summary>
        public const string VerifyEmail = "verify_email";

        /// <summary>Đặt lại mật khẩu khi người dùng quên mật khẩu.</summary>
        public const string ResetPassword = "reset_password";

        /// <summary>Xác thực email liên lạc MỚI khi trung tâm đổi email.</summary>
        public const string ChangeEmail = "change_email";

        /// <summary>[UC-80] Mã khôi phục tài khoản đang chờ xóa, cấp sau khi đăng nhập đúng mật khẩu.</summary>
        public const string RestoreAccount = "restore_account";
    }
}
