// Email đang chờ nhập OTP (đăng ký → xác thực, quên mật khẩu → đặt lại).
// Truyền qua sessionStorage thay vì ?email= để email không nằm trong URL, lịch sử trình duyệt và log máy chủ.

import { useSyncExternalStore } from "react";

const KEY = "usas_pending_email";

export function setPendingEmail(email: string) {
  try {
    sessionStorage.setItem(KEY, email);
  } catch {
    // Trình duyệt chặn storage: người dùng tự nhập email ở bước sau
  }
}

export function getPendingEmail(): string {
  try {
    return sessionStorage.getItem(KEY) ?? "";
  } catch {
    return "";
  }
}

// sessionStorage không phát sự kiện trong cùng tab nên không cần subscribe; server render luôn trả ""
const noopSubscribe = () => () => {};

export function usePendingEmail(): string {
  return useSyncExternalStore(noopSubscribe, getPendingEmail, () => "");
}

export function clearPendingEmail() {
  try {
    sessionStorage.removeItem(KEY);
  } catch {
    // bỏ qua
  }
}
