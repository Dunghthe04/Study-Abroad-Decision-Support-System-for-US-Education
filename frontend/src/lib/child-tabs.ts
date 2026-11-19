// Chức năng 1f: 4 thẻ của trang "Con của tôi", mở đúng thẻ qua ?tab= (trang hồ sơ riêng chuyển sang, menu)
export type ChildTab = "overview" | "recommend" | "targets" | "history" | "preference";

const TABS: readonly ChildTab[] = ["overview", "recommend", "targets", "history", "preference"];

/** Giá trị ?tab= hợp lệ thì dùng, lạ hoặc không có thì về "Tổng quan". */
export function toChildTab(value: string | string[] | undefined): ChildTab {
  const tab = Array.isArray(value) ? value[0] : value;
  return TABS.find((t) => t === tab) ?? "overview";
}
