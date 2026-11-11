import type { UniversityOffering } from "@/types/university";

export const MISSING_UNIVERSITY_DATA = "—";
const numbers = new Intl.NumberFormat("vi-VN");
const dollars = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 0 });
const percentages = new Intl.NumberFormat("vi-VN", { style: "percent", maximumFractionDigits: 1 });

export function universityNumber(value: number | null | undefined): string {
  return value == null || !Number.isFinite(value) ? MISSING_UNIVERSITY_DATA : numbers.format(value);
}

export function universityMoney(value: number | null | undefined): string {
  return value == null || !Number.isFinite(value) ? MISSING_UNIVERSITY_DATA : dollars.format(value);
}

export function universityPercent(value: number | null | undefined): string {
  return value == null || !Number.isFinite(value) || value < 0 || value > 1 ? MISSING_UNIVERSITY_DATA : percentages.format(value);
}

export function universityDate(value: string | null | undefined): string {
  if (!value) return MISSING_UNIVERSITY_DATA;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat("vi-VN", { dateStyle: "medium", timeZone: "UTC" }).format(date);
}

export function universityUrl(value: string | null | undefined): string | null {
  if (!value) return null;
  if (value.startsWith("/")) return value;
  try {
    const url = new URL(value);
    return url.protocol === "https:" || url.protocol === "http:" ? url.href : null;
  } catch {
    return null;
  }
}

export function universityHost(value: string): string {
  try { return new URL(value).hostname.replace(/^www\./, ""); } catch { return value; }
}

export function schoolControlLabel(value: string | null): string {
  return ({ public: "Công lập", private: "Tư thục" } as Record<string, string>)[value ?? ""] ?? MISSING_UNIVERSITY_DATA;
}

export function schoolImportanceLabel(value: string | null): string {
  return ({ very_important: "Rất quan trọng", important: "Quan trọng", considered: "Có xem xét", not_considered: "Không xem xét" } as Record<string, string>)[value ?? ""] ?? MISSING_UNIVERSITY_DATA;
}

export function schoolSatPolicy(value: string | null): string {
  return ({ required: "Yêu cầu SAT/ACT", optional: "Không bắt buộc SAT/ACT", not_accepted: "Không sử dụng SAT/ACT" } as Record<string, string>)[value ?? ""] ?? MISSING_UNIVERSITY_DATA;
}

export function schoolAidPolicy(value: string | null): string {
  return ({ none: "Không có hỗ trợ được ghi nhận", need_only: "Theo nhu cầu tài chính", merit_only: "Theo thành tích", need_and_merit: "Theo nhu cầu và thành tích", need_based: "Theo nhu cầu tài chính", merit_based: "Theo thành tích" } as Record<string, string>)[value ?? ""] ?? MISSING_UNIVERSITY_DATA;
}

export function scoreRange(low: number | null, high: number | null): string {
  if (low != null && high != null) return `${universityNumber(low)} – ${universityNumber(high)}`;
  if (low != null) return `Mốc 25%: ${universityNumber(low)}`;
  if (high != null) return `Mốc 75%: ${universityNumber(high)}`;
  return MISSING_UNIVERSITY_DATA;
}

export function annualPublishedCost(offering: UniversityOffering): number | null {
  const values = [offering.tuitionUsd, offering.livingUsd, offering.feesUsd, offering.otherExpensesUsd];
  if (values.some((value) => value == null || !Number.isFinite(value))) return null;
  return values.reduce<number>((total, value) => total + (value ?? 0), 0);
}

export function universityDeadlines(value: string | null): { round: string; label: string; date: string }[] {
  if (!value) return [];
  const labels: Record<string, string> = { ED: "Early Decision", ED2: "Early Decision II", EA: "Early Action", RD: "Regular Decision", Rolling: "Rolling Admission" };
  try {
    const parsed: unknown = JSON.parse(value);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return [];
    const result: { round: string; label: string; date: string }[] = [];
    for (const [round, date] of Object.entries(parsed)) {
      if (typeof date === "string" && date.trim()) result.push({ round, label: labels[round] ?? round, date: universityDate(date) });
    }
    return result;
  } catch {
    return [];
  }
}

export const UNIVERSITY_FIELD_LABELS: Record<string, string> = {
  name: "Tên trường", city: "Thành phố", state: "Bang", website: "Website", control: "Loại trường", is_ivy: "Ivy League",
  acceptance_rate: "Tỷ lệ nhận", international_student_count: "Số sinh viên quốc tế", tuition_usd: "Học phí", living_usd: "Sinh hoạt phí", fees_usd: "Phí bắt buộc", other_expenses_usd: "Chi phí khác", cost_year: "Năm chi phí", academic_year: "Năm học",
  min_gpa4: "GPA tối thiểu", avg_gpa4: "GPA trung bình", min_ielts: "IELTS", min_toefl: "TOEFL", min_duolingo: "Duolingo",
  sat25: "SAT mốc 25%", sat75: "SAT mốc 75%", act25: "ACT mốc 25%", act75: "ACT mốc 75%", sat_policy: "Chính sách SAT/ACT", sat_submit_pct: "Tỷ lệ nộp SAT", gpa_submit_pct: "Tỷ lệ nộp GPA",
  applicants_total: "Số hồ sơ nộp", admitted_total: "Số hồ sơ được nhận", intl_applicants: "Hồ sơ quốc tế", intl_admitted: "Hồ sơ quốc tế được nhận", intl_acceptance_rate: "Tỷ lệ nhận quốc tế",
  c7_gpa_importance: "Mức coi trọng GPA", c7_test_importance: "Mức coi trọng điểm thi", c7_ec_importance: "Mức coi trọng ngoại khóa",
  intl_aid_policy: "Chính sách hỗ trợ tài chính", intl_aid_avg_usd: "Hỗ trợ tài chính trung bình", intl_aid_recipients: "Số người nhận hỗ trợ", vn_aid_scope: "Phạm vi hỗ trợ sinh viên Việt Nam",
  retention_rate_1yr: "Tỷ lệ tiếp tục học sau năm đầu", grad_rate_6yr: "Tỷ lệ tốt nghiệp 6 năm", deadline_ed: "Hạn ED", deadline_ea: "Hạn EA", deadline_rd: "Hạn RD", deadlines_json: "Hạn nộp hồ sơ", majors: "Ngành học", stem_majors: "Ngành STEM", coa_total_usd: "Tổng chi phí công bố",
  gpa_pct_4_0: "Phân bố GPA 4,0", gpa_pct_375_399: "Phân bố GPA 3,75–3,99", gpa_pct_350_374: "Phân bố GPA 3,50–3,74", gpa_pct_325_349: "Phân bố GPA 3,25–3,49", gpa_pct_300_324: "Phân bố GPA 3,00–3,24", gpa_pct_250_299: "Phân bố GPA 2,50–2,99", gpa_pct_below_250: "Phân bố GPA dưới 2,50",
};
