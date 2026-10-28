// [USAS-640] Danh mục lựa chọn dùng chung cho luồng hồ sơ 6 bước.
// Giá trị (`value`) là mã lưu vào DB — khớp seed `profile.metadata` và ProfileConstants phía BE.

import type {
  AcademicYearDraft,
  ActivityRole,
  BudgetTierId,
  CurrentEducationLevel,
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

// Helper tính GPA thang 4 từ thang 10 theo công thức tuyến tính: GPA 4.0 = GPA 10 / 10 * 4.0
export function calculateLinearGpa(years: AcademicYearDraft[]): { gpa10: number | null; gpa4: number | null } {
  let totalScore10 = 0;
  let count = 0;

  for (const yr of years) {
    for (const sub of yr.subjects) {
      const s1 = typeof sub.semester1Score === "number" ? sub.semester1Score : null;
      const s2 = typeof sub.semester2Score === "number" ? sub.semester2Score : null;

      let subAvg: number | null = null;
      if (s1 !== null && s2 !== null) {
        subAvg = (s1 + s2) / 2;
      } else if (s1 !== null) {
        subAvg = s1;
      } else if (s2 !== null) {
        subAvg = s2;
      }

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

export function toGpa4(gpa10: number): number {
  return Math.round(((gpa10 / 10) * 4.0) * 100) / 100;
}

/** Cách nhập GPA của nháp; nháp cũ chưa có gpaMode thì có điểm môn là học bạ (giống TranscriptGpa.Mode phía BE) */
export function gpaModeOf(step2: TranscriptDraft | undefined): GpaMode {
  if (step2?.gpaMode === "overall" || step2?.gpaMode === "transcript") return step2.gpaMode;
  return calculateLinearGpa(step2?.academicYears ?? []).gpa10 === null ? "overall" : "transcript";
}

/** GPA của nháp theo cách nhập (giống TranscriptGpa.Gpa4 phía BE); GPA tổng nhập thang 4 thì gpa10 là null */
export function draftGpa(step2: TranscriptDraft | undefined): { gpa10: number | null; gpa4: number | null } {
  if (gpaModeOf(step2) === "transcript") return calculateLinearGpa(step2?.academicYears ?? []);
  const manual10 = step2?.manualGpa10Scale ?? null;
  if (manual10 !== null) return { gpa10: manual10, gpa4: toGpa4(manual10) };
  return { gpa10: null, gpa4: step2?.calculatedGpa4Scale ?? null };
}
