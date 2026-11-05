// Trang làm việc dùng khung rộng như demo; các trang đọc giữ bề rộng cũ
const WIDE_PREFIXES = ["/analysis", "/recommendations", "/admin", "/university/"];

export function isWideRoute(pathname: string) {
  return WIDE_PREFIXES.some((p) => pathname.startsWith(p));
}

// Trang tự quản lý lề và độ rộng (ví dụ full bleed banner hoặc thanh dính mép)
const FULL_PREFIXES = ["/recommendations", "/university/"];

export function isFullRoute(pathname: string) {
  return FULL_PREFIXES.some((p) => pathname.startsWith(p));
}
