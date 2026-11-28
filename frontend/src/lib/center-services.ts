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
  HOC_BONG: "Săn học bổng",
  LUYEN_THI_SAT: "Luyện thi SAT",
  LUYEN_THI_IELTS: "Luyện thi IELTS",
  HOAT_DONG_NGOAI_KHOA: "Xây dựng hoạt động ngoại khóa",
  HO_SO_VISA: "Hồ sơ & phỏng vấn visa F-1",
  CHUNG_MINH_TAI_CHINH: "Chứng minh tài chính",
  DINH_CU_VIEC_LAM: "Định hướng OPT & việc làm",
};

export function centerServiceLabel(code: string): string {
  return CENTER_SERVICES[code] ?? code.replace(/_/g, " ").toLowerCase();
}
