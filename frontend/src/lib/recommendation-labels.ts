// Chuyển mã của kết quả gợi ý (nhóm, mã lý do, cờ thiếu dữ liệu) thành chữ cho màn kết quả.

import type { FilterFlag, Pref, SchoolGroup, WhyCode } from "@/types/recommendation";
import { PREF_PRESETS, type PrefPreset } from "@/lib/recommendation-api";

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
};

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

// Tìm nút đang khớp với pref đã lưu; không khớp nút nào thì trả null
export function findPreset(pref: Pref | null | undefined): PrefPreset | null {
  if (!pref) return null;
  return PREF_PRESETS.find((p) => p.pref.every((v, i) => v === pref[i])) ?? null;
}

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
