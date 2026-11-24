// [UC-01 quy tắc mật khẩu] Danh sách mật khẩu phổ biến, dễ đoán; giống hệt backend
// (StudyAbroad.Application/Auth/CommonPasswords.cs). Backend vẫn kiểm lại khi gửi.
const COMMON_PASSWORDS = new Set<string>([
  "12345678", "123456789", "1234567890", "12341234", "11111111", "00000000", "88888888", "66666666", "87654321", "123123123", "password", "password1", "passw0rd", "p@ssw0rd", "p@ssword", "qwertyui", "qwerty123", "qwertyuiop", "1q2w3e4r", "1qaz2wsx", "zaq12wsx", "asdfghjk", "asdf1234", "abcd1234", "abc12345", "abcdefgh", "iloveyou", "iloveyou1", "sunshine", "princess", "football", "baseball", "superman", "starwars", "whatever", "trustno1", "letmein1", "welcome1", "welcome123", "admin123", "administrator", "changeme", "computer", "internet", "michelle", "jennifer", "matkhau123", "matkhau1", "anhyeuem", "anhyeuem1", "emyeuanh", "vietnam123", "hoilamgi", "khongbiet", "123456aa", "aa123456", "a1234567", "qwer1234", "1234qwer", "q1w2e3r4", "zxcvbnm1", "zxcvbnma", "11223344", "12121212", "10101010", "147258369", "123654789", "987654321", "123321123", "minhthu123", "hello123", "dragon12", "monkey12", "master12", "shadow12", "football1", "abc123456", "admin1234",
]);

export function isCommonPassword(password: string): boolean {
  return COMMON_PASSWORDS.has(password.trim().toLowerCase());
}

/** Lỗi mật khẩu theo UC-01: 8–100 ký tự, không toàn khoảng trắng, không thuộc danh sách phổ biến. */
export function passwordRuleError(password: string, label = "Mật khẩu"): string | null {
  if (!password) return `Vui lòng nhập ${label.toLowerCase()}.`;
  if (password.length < 8) return `${label} phải có tối thiểu 8 ký tự.`;
  if (password.length > 100) return `${label} không được vượt quá 100 ký tự.`;
  if (!password.trim()) return `${label} không được chỉ gồm khoảng trắng.`;
  if (isCommonPassword(password)) return `${label} quá phổ biến, dễ đoán. Vui lòng chọn mật khẩu khác.`;
  return null;
}
