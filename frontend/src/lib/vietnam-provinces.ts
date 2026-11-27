/**
 * Bản dự phòng 34 tỉnh/thành, chỉ dùng khi GET /api/v1/dashboard/provinces không trả được.
 * Nguồn chuẩn là backend (Domain/Constants/VietnamProvinces.cs): đổi ở đó thì đổi cả ở đây.
 */
export const FALLBACK_PROVINCES: string[] = [
  "Hà Nội", "Huế", "Lai Châu", "Điện Biên", "Sơn La", "Lạng Sơn",
  "Quảng Ninh", "Thanh Hóa", "Nghệ An", "Hà Tĩnh", "Cao Bằng", "Tuyên Quang",
  "Lào Cai", "Thái Nguyên", "Phú Thọ", "Bắc Ninh", "Hưng Yên", "Hải Phòng",
  "Ninh Bình", "Quảng Trị", "Đà Nẵng", "Quảng Ngãi", "Gia Lai", "Khánh Hòa",
  "Lâm Đồng", "Đắk Lắk", "TP. Hồ Chí Minh", "Đồng Nai", "Tây Ninh", "Cần Thơ",
  "Vĩnh Long", "Đồng Tháp", "Cà Mau", "An Giang",
];
