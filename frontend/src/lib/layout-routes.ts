// Trang làm việc dùng khung rộng như demo; các trang đọc giữ bề rộng cũ
const WIDE_PREFIXES = ["/analysis", "/recommendations", "/admin", "/centers", "/center", "/universities", "/university/", "/target-schools"];

export function isWideRoute(pathname: string) {
  if (pathname === "/") return true;
  return WIDE_PREFIXES.some((p) => pathname.startsWith(p));
}

// Trang tự quản lý lề và độ rộng (ví dụ full bleed banner hoặc thanh dính mép)
const FULL_PREFIXES = ["/recommendations", "/university/"];

export function isFullRoute(pathname: string) {
  if (pathname === "/") return true; // trang chủ: ảnh đầu trang tràn mép
  return FULL_PREFIXES.some((p) => pathname.startsWith(p));
}

// Trang xác thực tài khoản: ẩn thanh điều hướng và trợ lý, chia đôi màn hình
const AUTH_ROUTES = [
  "/login",
  "/register",
  "/forgot-password",
  "/reset-password",
  "/verify-email",
  "/parent-consent",
];

export function isAuthRoute(pathname: string) {
  return AUTH_ROUTES.some((route) => pathname === route || pathname.startsWith(`${route}/`));
}
