// Trang làm việc dùng khung rộng như demo; các trang đọc giữ bề rộng cũ
const WIDE_PREFIXES = ["/analysis", "/recommendations", "/admin", "/centers", "/university/", "/target-schools"];

export function isWideRoute(pathname: string) {
  return WIDE_PREFIXES.some((p) => pathname.startsWith(p));
}

// Trang tràn hết bề rộng, không căn giữa: thanh mô phỏng dính mép trái, danh sách chiếm phần còn lại sát khung chat
const FULL_PREFIXES = ["/recommendations"];

export function isFullRoute(pathname: string) {
  return FULL_PREFIXES.some((p) => pathname.startsWith(p));
}
