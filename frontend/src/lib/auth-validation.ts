// [UC-01] Kiểm lỗi dùng chung cho các form đăng ký, đăng nhập, OTP và mật khẩu.
// Giới hạn và câu báo lỗi giống backend (AuthService: ValidatePassword, IsValidEmail,
// TryValidateAndNormalizeVietnamesePhone, Max*Length). Backend vẫn kiểm lại khi gửi.

export const AUTH_LIMITS = {
  passwordMin: 8,
  passwordMax: 100,
  fullNameMax: 100,
  emailMax: 100,
  rawPhoneMax: 20,
  centerNameMax: 200,
} as const;

/** Lỗi khi không gọi được máy chủ (mất mạng, máy chủ không trả lời). */
export const NETWORK_ERROR = "Có lỗi kết nối máy chủ. Vui lòng thử lại sau.";

// ---------- Email ----------

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Lỗi ô email; trả null khi hợp lệ. Ô không bắt buộc mà để trống thì không lỗi. */
export function emailError(email: string, label = "Email", required = true): string | null {
  const value = email.trim();
  if (!value) return required ? `Vui lòng nhập ${label.toLowerCase()}.` : null;
  if (value.length > AUTH_LIMITS.emailMax) return `${label} không được vượt quá ${AUTH_LIMITS.emailMax} ký tự.`;
  if (!EMAIL_PATTERN.test(value)) return `${label} không đúng định dạng.`;
  return null;
}

// ---------- Họ tên, tên trung tâm ----------

/** Lỗi ô bắt buộc có giới hạn độ dài (họ tên, tên trung tâm...). */
export function requiredTextError(value: string, label: string, max: number): string | null {
  const text = value.trim();
  if (!text) return `Vui lòng nhập ${label.toLowerCase()}.`;
  if (text.length > max) return `${label} không được vượt quá ${max} ký tự.`;
  return null;
}

export function fullNameError(fullName: string, label = "Họ và tên"): string | null {
  return requiredTextError(fullName, label, AUTH_LIMITS.fullNameMax);
}

// ---------- Số điện thoại ----------

export const PHONE_FORMAT_ERROR =
  "Số điện thoại không đúng định dạng Việt Nam (ví dụ: 0912345678 hoặc +84912345678).";

/** Chuẩn hóa số di động Việt Nam về dạng 0xxxxxxxxx; sai định dạng trả null. */
export function normalizeVietnamesePhone(raw: string): string | null {
  let cleaned = raw.trim().replace(/[\s.\-()]/g, "");
  if (cleaned.startsWith("+84")) cleaned = "0" + cleaned.slice(3);
  else if (cleaned.startsWith("84") && cleaned.length === 11) cleaned = "0" + cleaned.slice(2);
  return /^0(3|5|7|8|9)\d{8}$/.test(cleaned) ? cleaned : null;
}

/** Kiểm ô số điện thoại. Hợp lệ thì trả số đã chuẩn hóa (null khi để trống ô không bắt buộc). */
export function checkPhone(raw: string, required = false): { error: string | null; normalized: string | null } {
  const value = raw.trim();
  if (!value) return { error: required ? "Vui lòng nhập số điện thoại." : null, normalized: null };
  if (value.length > AUTH_LIMITS.rawPhoneMax)
    return { error: `Số điện thoại không được vượt quá ${AUTH_LIMITS.rawPhoneMax} ký tự.`, normalized: null };
  const normalized = normalizeVietnamesePhone(value);
  return normalized ? { error: null, normalized } : { error: PHONE_FORMAT_ERROR, normalized: null };
}

// ---------- OTP ----------

/** Lỗi ô mã OTP: đúng 6 chữ số. */
export function otpError(code: string): string | null {
  return /^\d{6}$/.test(code.trim()) ? null : "Mã OTP phải gồm đúng 6 chữ số.";
}

// ---------- Mật khẩu ----------

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
  if (password.length < AUTH_LIMITS.passwordMin) return `${label} phải có tối thiểu ${AUTH_LIMITS.passwordMin} ký tự.`;
  if (password.length > AUTH_LIMITS.passwordMax) return `${label} không được vượt quá ${AUTH_LIMITS.passwordMax} ký tự.`;
  if (!password.trim()) return `${label} không được chỉ gồm khoảng trắng.`;
  const missing = missingPasswordParts(password);
  if (missing.length > 0)
    return `${label} phải có ít nhất 1 chữ hoa, 1 chữ thường, 1 chữ số và 1 ký tự đặc biệt (còn thiếu: ${missing.join(", ")}).`;
  return null;
}

/** Lỗi ô xác nhận mật khẩu. */
export function confirmPasswordError(password: string, confirm: string): string | null {
  if (!confirm) return "Vui lòng xác nhận mật khẩu.";
  if (password && confirm !== password) return "Mật khẩu xác nhận không khớp.";
  return null;
}
