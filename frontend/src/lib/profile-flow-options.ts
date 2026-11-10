// [USAS-640] Danh mục lựa chọn dùng chung cho luồng hồ sơ 6 bước.
// Giá trị (`value`) là mã lưu vào DB — khớp seed `profile.metadata` và ProfileConstants phía BE.

import type {
  AcademicYearDraft,
  ActivityRole,
  BudgetTierId,
  CurrentEducationLevel,
  EducationSystem,
  FundingSource,
  GpaMode,
  ImpactLevel,
  ScholarshipNeed,
  TargetSchoolType,
  TranscriptDraft,
} from "@/types/profile-flow";

export const EDUCATION_LEVEL_OPTIONS: { value: CurrentEducationLevel; label: string }[] = [
  { value: "middle_school", label: "Trung học cơ sở" },
  { value: "high_school", label: "Trung học phổ thông" },
  { value: "college_university", label: "Cao đẳng / Đại học" },
  { value: "master", label: "Thạc sĩ" },
];

/** Hệ chương trình cấp 3 — chữ giống seed `profile.metadata` (educationSystems), chat dùng cùng chữ trên nút */
export const EDUCATION_SYSTEM_OPTIONS: { value: EducationSystem; label: string }[] = [
  { value: "standard", label: "Hệ chuẩn công lập Việt Nam" },
  { value: "specialized", label: "Hệ chuyên (Trường THPT Chuyên / Năng khiếu)" },
  { value: "dual_degree", label: "Hệ song bằng (Việt - Anh / Cambridge)" },
  { value: "international", label: "Hệ quốc tế hoàn toàn (IB, AP, Cambridge)" },
  { value: "private", label: "Dân lập / Tư thục chất lượng cao" },
  { value: "other", label: "Hệ khác" },
];

/** Ai chi trả chính — chữ giống seed `profile.metadata` (fundingSources), chat dùng cùng chữ trên nút */
export const FUNDING_SOURCE_OPTIONS: { value: FundingSource; label: string }[] = [
  { value: "family_support", label: "Gia đình hỗ trợ toàn bộ / một phần" },
  { value: "personal_savings", label: "Tiết kiệm cá nhân" },
  { value: "bank_loan", label: "Vay vốn du học ngân hàng" },
  { value: "scholarship", label: "Trông đợi vào học bổng" },
  { value: "other", label: "Nguồn tài chính khác" },
];

export const SCHOOL_TYPE_OPTIONS: { value: TargetSchoolType; label: string }[] = [
  { value: "all", label: "Không lọc" },
  { value: "public", label: "Công lập" },
  { value: "private", label: "Tư thục" },
];

export function getGradeOptionsForLevel(level?: CurrentEducationLevel | null): string[] {
  switch (level) {
    case "middle_school":
      return ["Lớp 6", "Lớp 7", "Lớp 8", "Lớp 9"];
    case "high_school":
      return ["Lớp 10", "Lớp 11", "Lớp 12"];
    case "college_university":
      return ["Năm 1", "Năm 2", "Năm 3", "Năm 4", "Năm cuối", "Đã tốt nghiệp"];
    case "master":
      return ["Năm 1", "Năm 2", "Đã tốt nghiệp"];
    default:
      return [];
  }
}

/** Mức ngân sách dự phòng khi chưa tải được metadata (min/max chính thức lấy từ seed ở BE). */
export const DEFAULT_BUDGET_TIERS: { key: BudgetTierId; label: string; description: string }[] = [
  { key: "tier_under_20k", label: "Dưới 20.000 USD", description: "Học phí & sinh hoạt phí dưới $20.000/năm" },
  { key: "tier_20k_35k", label: "20.000 – 35.000 USD", description: "Học phí & sinh hoạt phí $20.000 – $35.000/năm" },
  { key: "tier_35k_50k", label: "35.000 – 50.000 USD", description: "Học phí & sinh hoạt phí $35.000 – $50.000/năm" },
  { key: "tier_50k_70k", label: "50.000 – 70.000 USD", description: "Học phí & sinh hoạt phí $50.000 – $70.000/năm" },
  { key: "tier_above_70k", label: "Trên 70.000 USD", description: "Học phí & sinh hoạt phí trên $70.000/năm" },
];

// Tỉ giá chỉ dùng để quy đổi trên form và ô nhập tiền của chat; số tiền không được lưu, chỉ lưu mã mức
export const BUDGET_RATE = 25000;
// Ngưỡng trên (USD) của từng mức khi metadata không có minUsd/maxUsd
export const FALLBACK_MAX_USD: Partial<Record<BudgetTierId, number>> = {
  tier_under_20k: 20000,
  tier_20k_35k: 35000,
  tier_35k_50k: 50000,
  tier_50k_70k: 70000,
};

export const SCHOLARSHIP_NEED_OPTIONS: { value: ScholarshipNeed; label: string; description: string }[] = [
  { value: "mandatory", label: "Bắt buộc", description: "Không có học bổng thì chưa thể đi" },
  { value: "preferred", label: "Nên có", description: "Càng nhiều càng tốt" },
  { value: "optional", label: "Không cần", description: "Gia đình tự chi trả được" },
];

/** Phạm vi ảnh hưởng — thứ tự = impact_level 1..5 trong DB. */
export const IMPACT_LEVEL_OPTIONS: { value: ImpactLevel; label: string }[] = [
  { value: "school", label: "Trường" },
  { value: "district", label: "Quận / Huyện" },
  { value: "province", label: "Tỉnh / Thành phố" },
  { value: "national", label: "Quốc gia" },
  { value: "international", label: "Quốc tế" },
];

export interface ImpactTierDetail {
  value: ImpactLevel;
  tierNumber: number; // 1 (đáy) -> 5 (đỉnh)
  label: string;
  shortLabel: string;
  badge: string;
  icon: string;
  summary: string;
  activityCriteria: string[];
  awardCriteria: string[];
}

export const IMPACT_TIER_DETAILS: Record<ImpactLevel, ImpactTierDetail> = {
  international: {
    value: "international",
    tierNumber: 5,
    label: "Cấp Quốc tế",
    shortLabel: "Quốc tế",
    badge: "Xuất sắc / Đỉnh cao",
    icon: "🌍",
    summary: "Quy mô toàn cầu, các cuộc thi, kỳ thi Olympic hoặc diễn đàn quốc tế lớn.",
    activityCriteria: [
      "Đại diện quốc gia tham gia các diễn đàn, hội nghị quốc tế (Model UN thế giới, thanh niên ASEAN...).",
      "Sáng lập hoặc điều hành dự án quốc tế, sản phẩm công nghệ có người dùng đa quốc gia.",
    ],
    awardCriteria: [
      "Huy chương Olympic quốc tế (IMO, IOI, IPhO, IChO, IBO...).",
      "Giải thưởng nghiên cứu khoa học quốc tế (ISEF, Genius Olympiad...).",
      "Giải thưởng cuộc thi nghệ thuật, thể thao toàn cầu uy tín.",
    ],
  },
  national: {
    value: "national",
    tierNumber: 4,
    label: "Cấp Quốc gia",
    shortLabel: "Quốc gia",
    badge: "Rất mạnh / Toàn quốc",
    icon: "🇻🇳",
    summary: "Quy mô cấp quốc gia do các Bộ, Ban, Ngành hoặc tổ chức toàn quốc chủ trì.",
    activityCriteria: [
      "Lãnh đạo dự án hoặc tổ chức phi lợi nhuận hoạt động trên 3 tỉnh/thành phố.",
      "Thực tập sinh hoặc tham gia nghiên cứu cấp viện nghiên cứu, tập đoàn lớn.",
      "Đại biểu thanh niên, tình nguyện viên tiêu biểu cấp toàn quốc.",
    ],
    awardCriteria: [
      "Giải Học sinh Giỏi Quốc gia (HSGQG do Bộ GD&ĐT tổ chức).",
      "Giải Cuộc thi Khoa học Kỹ thuật cấp Quốc gia (ViSEF).",
      "Huy chương giải vô địch thể thao, văn nghệ toàn quốc.",
    ],
  },
  province: {
    value: "province",
    tierNumber: 3,
    label: "Cấp Tỉnh / Thành phố",
    shortLabel: "Tỉnh / TP",
    badge: "Mạnh / Địa phương",
    icon: "🏙️",
    summary: "Quy mô cấp tỉnh hoặc thành phố trực thuộc Trung ương (Hà Nội, TP.HCM, Đà Nẵng...).",
    activityCriteria: [
      "Sáng lập hoặc chủ nhiệm CLB, dự án thiện nguyện quy mô toàn tỉnh/thành phố.",
      "Tổ chức sự kiện lớn thu hút từ 100 người tham gia trở lên ngoài phạm vi trường.",
    ],
    awardCriteria: [
      "Giải Học sinh Giỏi cấp Tỉnh / Thành phố trực thuộc Trung ương.",
      "Giải thưởng thi đấu thể thao, giải nghệ thuật cấp Tỉnh / Thành phố.",
      "Giải thưởng sáng tạo khoa học kỹ thuật cấp Tỉnh / Thành phố.",
    ],
  },
  district: {
    value: "district",
    tierNumber: 2,
    label: "Cấp Quận / Huyện",
    shortLabel: "Quận / Huyện",
    badge: "Khá / Cụm trường",
    icon: "🏘️",
    summary: "Quy mô giao lưu, thi đấu giữa các trường trong quận/huyện hoặc cụm trường.",
    activityCriteria: [
      "Lãnh đạo hoặc điều phối dự án liên trường trong địa bàn quận/huyện.",
      "Thành viên ban tổ chức chiến dịch tình nguyện, dự án cộng đồng địa phương.",
    ],
    awardCriteria: [
      "Giải Học sinh Giỏi cấp Quận / Huyện / Cụm trường.",
      "Giải thi đấu phong trào thể thao, hội thi văn nghệ cấp Quận / Huyện.",
    ],
  },
  school: {
    value: "school",
    tierNumber: 1,
    label: "Cấp Trường",
    shortLabel: "Trường",
    badge: "Tiêu chuẩn / Cơ bản",
    icon: "🏫",
    summary: "Các hoạt động, câu lạc bộ và giải thưởng nội bộ trong trường học.",
    activityCriteria: [
      "Ban chủ nhiệm, trưởng ban hoặc thành viên tích cực của câu lạc bộ trường.",
      "Tham gia ban tổ chức các sự kiện trường (Prom, Lễ hội, Ngày hội thông tin...).",
      "Hoạt động tình nguyện hoặc trách nhiệm nội bộ trường học.",
    ],
    awardCriteria: [
      "Giải Học sinh Giỏi cấp trường / giải Olympic cấp trường.",
      "Giải thi đấu phong trào thể thao, văn nghệ, giải học thuật cấp trường.",
      "Học bổng khuyến khích hoặc danh hiệu học sinh tiêu biểu của trường.",
    ],
  },
};

/** Mảng 5 cấp xếp theo thứ tự Kim tự tháp từ Đỉnh (Quốc tế) xuống Đáy (Trường) */
export const IMPACT_PYRAMID_LEVELS: ImpactLevel[] = [
  "international",
  "national",
  "province",
  "district",
  "school",
];

export const ACTIVITY_ROLE_OPTIONS: { value: ActivityRole; label: string }[] = [
  { value: "founder", label: "Sáng lập" },
  { value: "leader", label: "Trưởng / Chủ nhiệm" },
  { value: "vice_leader", label: "Phó" },
  { value: "member", label: "Thành viên" },
];

export function labelOf<T extends string>(options: { value: T; label: string }[], value?: T | null): string {
  return options.find((o) => o.value === value)?.label ?? "";
}

/** "MM/yyyy" → { year, month } hoặc null nếu sai định dạng. */
export function parseMonthYear(input?: string | null): { year: number; month: number } | null {
  const m = /^\s*(\d{1,2})\s*\/\s*(\d{4})\s*$/.exec(input ?? "");
  if (!m) return null;
  const month = Number(m[1]);
  const year = Number(m[2]);
  if (month < 1 || month > 12 || year < 1900) return null;
  return { year, month };
}

/** Số tháng tính cả 2 đầu (09/2024 → 05/2026 = 21). Khớp ProfileFlowService.CountMonths. */
export function countMonths(start: string, end: string | null, isOngoing: boolean, today = new Date()): number | null {
  const s = parseMonthYear(start);
  const e = isOngoing ? { year: today.getFullYear(), month: today.getMonth() + 1 } : parseMonthYear(end);
  if (!s || !e) return null;
  return Math.max(1, (e.year - s.year) * 12 + (e.month - s.month) + 1);
}

/** Tách "Lớp 12 HK1" / "Lớp 12 - HK2" thành khối + học kỳ. Khớp regex TermNameRegex phía BE. */
export function parseTermName(termName: string): { gradeName: string; semester: 1 | 2 } | null {
  const m = /^(.*?)\s*-?\s*(?:HK|Học kỳ|Kỳ)\s*([12])\s*$/i.exec(termName.trim());
  if (!m) return null;
  return { gradeName: m[1].trim(), semester: m[2] === "2" ? 2 : 1 };
}

/**
 * TB cả năm một môn theo cách tính Việt Nam: HK2 hệ số 2 → (HK1 + 2×HK2) / 3. Chỉ có một kỳ thì lấy kỳ đó,
 * không có kỳ nào thì null. Không làm tròn ở đây (giống TranscriptGpa.YearAverage phía BE)
 */
export function yearAverage(s1: number | null, s2: number | null): number | null {
  if (s1 !== null && s2 !== null) return (s1 + 2 * s2) / 3;
  return s1 ?? s2;
}

// TB thang 10 của các môn (TB cả năm từng môn) và GPA thang 4 quy đổi theo bảng đang dùng (toGpa4)
export function calculateTranscriptGpa(years: AcademicYearDraft[]): { gpa10: number | null; gpa4: number | null } {
  let totalScore10 = 0;
  let count = 0;

  for (const yr of years) {
    for (const sub of yr.subjects) {
      const s1 = typeof sub.semester1Score === "number" ? sub.semester1Score : null;
      const s2 = typeof sub.semester2Score === "number" ? sub.semester2Score : null;
      const subAvg = yearAverage(s1, s2);

      if (subAvg !== null && subAvg >= 0 && subAvg <= 10) {
        totalScore10 += subAvg;
        count++;
      }
    }
  }

  if (count === 0) return { gpa10: null, gpa4: null };

  const gpa10 = Math.round((totalScore10 / count) * 100) / 100;
  const gpa4 = toGpa4(gpa10);

  return { gpa10, gpa4 };
}

/**
 * Bảng WES mặc định quy đổi điểm thang 10 sang GPA thang 4: [điểm từ, GPA 4], khớp AcademicAnalysisService.
 * GetDefaultWesScaleConfig (BE). Chỉ dùng khi chưa tải được bảng grade_scale.10 từ API (moderator sửa trên trang
 * quản trị), xem setGradeScaleRules / useGradeScale. Chỉ xét cận dưới: 8.99 vẫn là 3.7
 */
export const WES_GPA10_TO_4: readonly (readonly [number, number])[] = [
  [9.0, 4.0],
  [8.5, 3.7],
  [8.0, 3.5],
  [7.0, 3.0],
  [6.5, 2.5],
  [5.5, 2.0],
  [5.0, 1.5],
  [4.0, 1.0],
];

type Gpa10To4Table = readonly (readonly [number, number])[];

// Bảng đang dùng: WES mặc định cho tới khi useGradeScale tải xong bảng từ API
let activeGpa10To4: Gpa10To4Table = WES_GPA10_TO_4;
const gradeScaleListeners = new Set<() => void>();

/**
 * Thay bảng quy đổi bằng bảng lấy từ API (rules: minScore, gpa4). Bỏ qua bảng rỗng hoặc có số không hợp lệ,
 * khi đó giữ bảng đang dùng. Báo cho các component đang dùng useGradeScale vẽ lại
 */
export function setGradeScaleRules(rules: readonly { minScore: number; gpa4: number }[]): void {
  const valid = rules.length > 0 && rules.every((r) => Number.isFinite(r.minScore) && Number.isFinite(r.gpa4));
  if (!valid) return;
  activeGpa10To4 = [...rules].sort((a, b) => b.minScore - a.minScore).map((r) => [r.minScore, r.gpa4] as const);
  gradeScaleListeners.forEach((listener) => listener());
}

/** Bảng đang dùng (để useGradeScale biết khi nào bảng đổi) */
export function getGradeScaleTable(): Gpa10To4Table {
  return activeGpa10To4;
}

export function subscribeGradeScale(listener: () => void): () => void {
  gradeScaleListeners.add(listener);
  return () => gradeScaleListeners.delete(listener);
}

export function toGpa4(gpa10: number): number {
  for (const [cut, gpa4] of activeGpa10To4) {
    if (gpa10 >= cut) return gpa4;
  }
  return 0;
}

/** Cách nhập GPA của nháp; nháp cũ chưa có gpaMode thì có điểm môn là học bạ (giống TranscriptGpa.Mode phía BE) */
export function gpaModeOf(step2: TranscriptDraft | undefined): GpaMode {
  if (step2?.gpaMode === "overall" || step2?.gpaMode === "transcript") return step2.gpaMode;
  return calculateTranscriptGpa(step2?.academicYears ?? []).gpa10 === null ? "overall" : "transcript";
}

/** GPA của nháp theo cách nhập (giống TranscriptGpa.Gpa4 phía BE); GPA tổng nhập thang 4 thì gpa10 là null */
export function draftGpa(step2: TranscriptDraft | undefined): { gpa10: number | null; gpa4: number | null } {
  if (gpaModeOf(step2) === "transcript") return calculateTranscriptGpa(step2?.academicYears ?? []);
  const manual10 = step2?.manualGpa10Scale ?? null;
  if (manual10 !== null) return { gpa10: manual10, gpa4: toGpa4(manual10) };
  return { gpa10: null, gpa4: step2?.calculatedGpa4Scale ?? null };
}

export type GradeTrendType = "strong_upward" | "slight_upward" | "stable" | "downward";

export interface GradeTrendInfo {
  hasEnoughData: boolean;
  trend: GradeTrendType | null;
  changeGpa10: number;
  yearlyAverages: { gradeName: string; gpa10: number; gpa4: number }[];
  summaryMessage: string;
}

/** Đánh giá xu hướng tiến bộ học tập (Grade Trend / Upward Trend) theo chuẩn tuyển sinh Mỹ */
export function calculateGradeTrend(years: AcademicYearDraft[]): GradeTrendInfo {
  const yearlyAverages: { gradeName: string; gpa10: number; gpa4: number }[] = [];

  for (const yr of years) {
    let sum = 0;
    let count = 0;
    for (const sub of yr.subjects) {
      const s1 = typeof sub.semester1Score === "number" ? sub.semester1Score : null;
      const s2 = typeof sub.semester2Score === "number" ? sub.semester2Score : null;
      const avg = yearAverage(s1, s2);

      if (avg !== null && avg >= 0 && avg <= 10) {
        sum += avg;
        count++;
      }
    }
    if (count > 0) {
      const gpa10 = Math.round((sum / count) * 100) / 100;
      yearlyAverages.push({
        gradeName: yr.gradeName,
        gpa10,
        gpa4: toGpa4(gpa10),
      });
    }
  }

  if (yearlyAverages.length < 2) {
    return {
      hasEnoughData: false,
      trend: null,
      changeGpa10: 0,
      yearlyAverages,
      summaryMessage: "",
    };
  }

  const firstGpa = yearlyAverages[0].gpa10;
  const latestGpa = yearlyAverages[yearlyAverages.length - 1].gpa10;
  const changeGpa10 = Math.round((latestGpa - firstGpa) * 100) / 100;

  let trend: GradeTrendType = "stable";
  let summaryMessage = "Thành tích học tập duy trì ổn định qua các năm học.";

  if (changeGpa10 >= 0.3) {
    trend = "strong_upward";
    summaryMessage = `Điểm số tăng mạnh (+${changeGpa10}) qua các năm — Điểm cộng lớn trong mắt hội đồng tuyển sinh Mỹ!`;
  } else if (changeGpa10 >= 0.1) {
    trend = "slight_upward";
    summaryMessage = `Điểm số có xu hướng tiến bộ (+${changeGpa10}) — Cho thấy đà phát triển và nỗ lực học tập tốt.`;
  } else if (changeGpa10 <= -0.3) {
    trend = "downward";
    summaryMessage = `Điểm số có xu hướng giảm (${changeGpa10}) — Bạn nên chú ý cải thiện điểm số ở các kỳ tới.`;
  } else if (changeGpa10 <= -0.1) {
    trend = "downward";
    summaryMessage = `Điểm số giảm nhẹ (${changeGpa10}) — Hãy duy trì sự tập trung để phục hồi phong độ.`;
  }

  return {
    hasEnoughData: true,
    trend,
    changeGpa10,
    yearlyAverages,
    summaryMessage,
  };
}

