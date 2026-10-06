namespace StudyAbroad.Domain.Constants
{
    public static class OtpPurposes
    {
        /// <summary>Xác thực địa chỉ email khi đăng ký tài khoản mới.</summary>
        public const string VerifyEmail = "verify_email";

        /// <summary>Đặt lại mật khẩu khi người dùng quên mật khẩu.</summary>
        public const string ResetPassword = "reset_password";

        /// <summary>Mở khóa tài khoản tạm thời khi bị khóa do gõ sai 5 lần liên tiếp.</summary>
        public const string UnlockAccount = "unlock_account";
    }
}
