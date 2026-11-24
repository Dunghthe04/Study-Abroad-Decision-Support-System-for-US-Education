namespace StudyAbroad.Application.Auth
{
    /// <summary>
    /// [UC-01 quy tắc mật khẩu] Danh sách mật khẩu phổ biến, dễ đoán bị từ chối khi đăng ký, đặt lại hoặc đổi mật khẩu.
    /// So không phân biệt hoa thường. Chỉ giữ mật khẩu từ 8 ký tự (ngắn hơn đã bị luật độ dài chặn).
    /// Frontend có bản giống hệt ở frontend/src/lib/common-passwords.ts để báo lỗi ngay dưới ô nhập.
    /// </summary>
    public static class CommonPasswords
    {
        private static readonly HashSet<string> List = new(StringComparer.OrdinalIgnoreCase)
        {
            "12345678", "123456789", "1234567890", "12341234", "11111111", "00000000", "88888888", "66666666", "87654321", "123123123", "password", "password1", "passw0rd", "p@ssw0rd", "p@ssword", "qwertyui", "qwerty123", "qwertyuiop", "1q2w3e4r", "1qaz2wsx", "zaq12wsx", "asdfghjk", "asdf1234", "abcd1234", "abc12345", "abcdefgh", "iloveyou", "iloveyou1", "sunshine", "princess", "football", "baseball", "superman", "starwars", "whatever", "trustno1", "letmein1", "welcome1", "welcome123", "admin123", "administrator", "changeme", "computer", "internet", "michelle", "jennifer", "matkhau123", "matkhau1", "anhyeuem", "anhyeuem1", "emyeuanh", "vietnam123", "hoilamgi", "khongbiet", "123456aa", "aa123456", "a1234567", "qwer1234", "1234qwer", "q1w2e3r4", "zxcvbnm1", "zxcvbnma", "11223344", "12121212", "10101010", "147258369", "123654789", "987654321", "123321123", "minhthu123", "hello123", "dragon12", "monkey12", "master12", "shadow12", "football1", "abc123456", "admin1234"
        };

        public static bool Contains(string password) => List.Contains(password.Trim());
    }
}
