// Ánh xạ mã dịch vụ (code) sang tên hiển thị thân thiện với người dùng (tiếng Việt có dấu).
// Đồng bộ với danh mục dịch vụ chuẩn trong hệ thống (advisor/app/services/centers.py).
export const CENTER_SERVICES: Record<string, string> = {
  CHON_TRUONG: "Tư vấn chọn trường",
  SUA_BAI_LUAN: "Sửa bài luận",
  NOP_HO_SO: "Nộp hồ sơ du học",
  LUYEN_PHONG_VAN: "Luyện phỏng vấn visa",
  LUYEN_THI: "Luyện thi IELTS / SAT",
  SAN_HOC_BONG: "Săn học bổng",
  CHUNG_CHI: "Luyện thi chứng chỉ",
  HO_SO: "Hồ sơ du học",
};

export function centerServiceLabel(code: string): string {
  return CENTER_SERVICES[code] ?? code.replace(/_/g, " ").toLowerCase();
}
