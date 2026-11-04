// Chuyển mã của kết quả gợi ý (nhóm, mã lý do, cờ thiếu dữ liệu) thành chữ cho màn kết quả.

import type { ChangeStatus, FilterFlag, SchoolGroup, WhyCode } from "@/types/recommendation";

// Tên nhóm hiện trên thẻ trường
export const GROUP_LABELS: Record<SchoolGroup, string> = {
  R: "Khó vào",
  RF: "Khó vào xa",
  M: "Vừa sức",
  S: "An toàn hơn",
  NA: "Chưa đủ dữ liệu",
  O: "Nhận gần như tất cả",
};

// Mã lý do → một câu ngắn
export const WHY_TEXT: Record<WhyCode, string> = {
  LOW_RATE: "Trường rất chọn lọc (nhận dưới 15% hồ sơ) nên luôn xếp Khó vào, dù điểm của bạn cao",
  RATE_CAP: "Trường nhận dưới 50% hồ sơ nên chưa tính là An toàn",
  WEAK_CAP: "Có một chỉ số thấp hơn mốc 25% của trường nên chưa tính là An toàn",
  GPA_HIGH: "GPA cao hơn phần lớn sinh viên của trường",
  GPA_MID: "GPA nằm trong khoảng giữa của trường",
  GPA_LOW: "GPA thấp hơn phần lớn sinh viên của trường",
  SAT_ABOVE: "SAT cao hơn mốc 75% của trường",
  SAT_IN_RANGE: "SAT nằm trong khoảng 25–75% của trường",
  SAT_BELOW: "SAT thấp hơn mốc 25% của trường",
  BUDGET_OK: "Chi phí nằm trong ngân sách",
  NEED_AID: "Vượt ngân sách, cần học bổng lớn",
  INTL_LOW: "Trường nhận rất ít du học sinh",
  EC_PLUS: "Ngoại khóa mạnh hơn mức trường kỳ vọng",
  EC_MINUS: "Ngoại khóa thấp hơn mức trường kỳ vọng",
};

// Cờ thiếu dữ liệu → dòng nhỏ dưới tên trường
export const FLAG_TEXT: Record<FilterFlag, string> = {
  MAJOR_UNKNOWN: "Chưa rõ trường có ngành bạn chọn",
  CONTROL_UNKNOWN: "Chưa rõ trường công lập hay tư thục",
  COST_UNKNOWN: "Chưa có số liệu chi phí",
  NEED_BIG_AID: "Cần học bổng lớn",
  AID_UNKNOWN: "Chưa rõ học bổng cho du học sinh",
  ENGLISH_NO_SCORE: "Bạn chưa nhập điểm tiếng Anh",
  ENGLISH_MIN_UNKNOWN: "Trường chưa công bố điểm tiếng Anh tối thiểu",
  ENGLISH_NOT_COMPARABLE: "Không so được điểm tiếng Anh với yêu cầu của trường",
  ENGLISH_BELOW_MIN: "Điểm tiếng Anh của bạn thấp hơn mức tối thiểu của trường",
};

// Mã lý do xếp nhóm (GroupReasons ở backend) → vì sao trường không đạt nhóm đích
export const GOAL_REASON_TEXT: Record<string, string> = {
  LOW_ACCEPT_RATE: "Trường chỉ nhận dưới 15% hồ sơ nên luôn xếp Khó vào, điểm cao đến đâu cũng không đổi nhóm.",
  CAPPED_SELECTIVE: "Trường nhận dưới 50% hồ sơ nên chỉ tối đa là Vừa sức, chưa tính là An toàn.",
  CAPPED_WEAK_METRIC: "Một chỉ số của bạn còn thấp hơn mốc 25% của trường nên chưa tính là An toàn.",
  INTL_DOWNGRADE: "Trường nhận du học sinh ít hơn hẳn tỷ lệ chung nên bị hạ một nhóm.",
  NEED_TEST: "Trường bắt buộc nộp điểm SAT hoặc ACT mà hồ sơ của bạn chưa có.",
  NO_ACADEMIC_DATA: "Trường chưa có đủ số liệu học thuật để so sánh với hồ sơ của bạn.",
};

export const FACTOR_LABELS: Record<string, string> = {
  sat: "SAT",
  gpa: "GPA (thang 4)",
  ecTier: "Bậc ngoại khóa",
};

export const DIFFICULTY_LABELS: Record<string, string> = {
  easy: "Khả thi",
  hard: "Khó",
  veryHard: "Rất khó",
};

export const GOAL_TARGET_OPTIONS: { value: "reach" | "match" | "safety"; label: string }[] = [
  { value: "match", label: "Vừa sức" },
  { value: "safety", label: "An toàn hơn" },
];

// Một giá trị của yếu tố: SAT và bậc là số nguyên, GPA hai chữ số thập phân
export function goalValue(factor: string, value: number | null): string {
  if (value == null) return "Chưa có";
  return factor === "gpa"
    ? value.toLocaleString("vi-VN", { minimumFractionDigits: 0, maximumFractionDigits: 2 })
    : Math.round(value).toLocaleString("vi-VN");
}

// Đổi mã sang chữ, mã lạ thì giữ nguyên để không mất thông tin
export const whyText = (code: string) => WHY_TEXT[code as WhyCode] ?? code;
export const flagText = (code: string) => FLAG_TEXT[code as FilterFlag] ?? code;
export const groupLabel = (g: string) => GROUP_LABELS[g as SchoolGroup] ?? g;

// Màu badge theo nhóm, dùng các variant đã có trong components/ui/badge.tsx
export function groupVariant(g: string): "risk" | "warn" | "ok" | "neutral" {
  if (g === "R" || g === "RF") return "risk";
  if (g === "M") return "warn";
  if (g === "S") return "ok";
  return "neutral";
}

// 95610 → "95.610 USD"
export function formatUsd(value: number | null | undefined): string {
  if (value == null) return "Chưa có số liệu";
  return `${Math.round(value).toLocaleString("vi-VN")} USD`;
}

// Trạng thái của một trường trong trình mô phỏng (nhóm, khối học bổng, hoặc bị loại)
export const STATUS_LABELS: Record<ChangeStatus, string> = {
  R: "Khó vào",
  M: "Vừa sức",
  S: "An toàn hơn",
  NA: "Chưa đủ dữ liệu",
  AID: "Cần học bổng lớn",
  OUT: "Ngoài danh sách",
};
export const statusLabel = (status: string) => STATUS_LABELS[status as ChangeStatus] ?? status;

// "https://www.stanford.edu/" → "stanford.edu". Chỉ nhận http/https, link lạ thì trả null.
export function siteHost(url: string | null | undefined): string | null {
  if (!url) return null;
  try {
    const u = new URL(url);
    if (u.protocol !== "http:" && u.protocol !== "https:") return null;
    return u.hostname.replace(/^www\./, "");
  } catch {
    return null;
  }
}
