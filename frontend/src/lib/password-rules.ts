// [UC-01 quy tắc mật khẩu] Giống hệt backend (AuthService.ValidatePassword). Backend vẫn kiểm lại khi gửi.

/** Gợi ý luật mật khẩu để hiện ở ô nhập. */
export const PASSWORD_RULE_HINT = "8–100 ký tự, có chữ hoa, chữ thường, chữ số và ký tự đặc biệt";

/** Các loại ký tự còn thiếu. Ký tự đặc biệt: không phải chữ, số hay khoảng trắng (vd ! @ # $ %). */
export function missingPasswordParts(password: string): string[] {
  const missing: string[] = [];
  if (!/\p{Lu}/u.test(password)) missing.push("chữ hoa");
  if (!/\p{Ll}/u.test(password)) missing.push("chữ thường");
  if (!/\p{Nd}/u.test(password)) missing.push("chữ số");
  if (!/[^\p{L}\p{Nd}\s]/u.test(password)) missing.push("ký tự đặc biệt");
  return missing;
}

/** Lỗi mật khẩu theo UC-01: 8–100 ký tự, không toàn khoảng trắng, đủ chữ hoa, chữ thường, chữ số, ký tự đặc biệt. */
export function passwordRuleError(password: string, label = "Mật khẩu"): string | null {
  if (!password) return `Vui lòng nhập ${label.toLowerCase()}.`;
  if (password.length < 8) return `${label} phải có tối thiểu 8 ký tự.`;
  if (password.length > 100) return `${label} không được vượt quá 100 ký tự.`;
  if (!password.trim()) return `${label} không được chỉ gồm khoảng trắng.`;
  const missing = missingPasswordParts(password);
  if (missing.length > 0)
    return `${label} phải có ít nhất 1 chữ hoa, 1 chữ thường, 1 chữ số và 1 ký tự đặc biệt (còn thiếu: ${missing.join(", ")}).`;
  return null;
}
