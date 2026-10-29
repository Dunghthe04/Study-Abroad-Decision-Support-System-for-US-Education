// Trang làm việc dùng khung rộng như demo; các trang đọc giữ bề rộng cũ
const WIDE_PREFIXES = ["/analysis", "/recommendations", "/admin"];

export function isWideRoute(pathname: string) {
  return WIDE_PREFIXES.some((p) => pathname.startsWith(p));
}
