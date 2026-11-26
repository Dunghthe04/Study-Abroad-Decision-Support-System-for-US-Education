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

        /// <summary>[UC-75 4b / UC-109 3b] Nút "Hủy xóa và đồng ý lại cho con" trong mail gửi người đại diện. Hết hạn khi tài khoản bị xóa hẳn.</summary>
        public const string ParentRestoreReconsent = "parent_restore_reconsent";

        /// <summary>[UC-75 4b] Nút "Chỉ hủy xóa" trong mail gửi người đại diện. Hết hạn khi tài khoản bị xóa hẳn.</summary>
        public const string ParentRestoreOnly = "parent_restore_only";

        /// <summary>[UC-03 3d] Mã khôi phục xác thực hai lớp, mỗi mã một dòng, dùng một lần. Không hết hạn (ExpiresAt đặt rất xa để job dọn không xóa).</summary>
        public const string RecoveryCode = "recovery_code";

        /// <summary>[UC-03 3d] Phiếu bước 2 khi Moderator, Administrator đã nhập đúng mật khẩu, hết hạn sau 5 phút.</summary>
        public const string LoginTwoFactor = "login_2fa";
    }
}
