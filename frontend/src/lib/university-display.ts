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
  // Đường dẫn trong site (ảnh trong public/). Chặn "//host" vì trình duyệt hiểu đó là link sang site khác
  if (value.startsWith("/") && !value.startsWith("//") && !value.startsWith("/\\")) return value;
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

export function schoolImportanceClass(value: string | null): string {
  if (value === "very_important" || value === "important") return "text-risk font-semibold";
  if (value === "considered") return "text-warn font-medium";
  return "text-ink-3";
}

export function campusSettingLabel(value: string | null): string {
  if (!value) return MISSING_UNIVERSITY_DATA;
  const map: Record<string, string> = {
    "City: Large": "Đô thị lớn",
    "City: Midsize": "Đô thị vừa",
    "City: Small": "Đô thị nhỏ",
    "Suburban: Large": "Ngoại ô lớn",
    "Suburban: Midsize": "Ngoại ô vừa",
    "Suburban: Small": "Ngoại ô nhỏ",
    "Town: Fringe": "Thị trấn ven đô",
    "Town: Distant": "Thị trấn",
    "Town: Remote": "Thị trấn xa",
    "Rural: Fringe": "Nông thôn ven đô",
    "Rural: Distant": "Nông thôn",
    "Rural: Remote": "Nông thôn xa",
  };
  return map[value] ?? value;
}

export function housingTypeLabel(value: string): string {
  const map: Record<string, string> = {
    "Coed residence halls": "Ký túc xá nam nữ chung",
    "Men's residence halls": "Ký túc xá nam",
    "Women's residence halls": "Ký túc xá nữ",
    "Apartments for married students": "Căn hộ cho sinh viên có gia đình",
    "Apartments for single students": "Căn hộ cho sinh viên độc thân",
    "Special housing for disabled students": "Khu nhà hỗ trợ SV khuyết tật",
    "Special housing for international students": "Khu nhà ở cho SV quốc tế",
    "Fraternity/sorority housing": "Nhà hội nam sinh / nữ sinh",
    "Cooperative housing": "Nhà ở mô hình hợp tác xã",
    "Theme housing": "Khu nhà theo chủ đề",
    "Wellness housing": "Khu nhà phong cách lành mạnh",
    "Living Learning Communities": "Cộng đồng học tập nội trú (LLC)",
    "Other housing options": "Các lựa chọn nhà ở khác",
  };
  return map[value] ?? value;
}

export function activityLabel(value: string): string {
  const map: Record<string, string> = {
    "Campus Ministries": "Mục vụ học đường",
    "Choral groups": "Dàn hợp xướng",
    "Concert band": "Ban nhạc hòa tấu",
    "Dance": "Câu lạc bộ khiêu vũ / vũ đạo",
    "Drama/theater": "Kịch nghệ & sân khấu",
    "International Student Organization": "Hội sinh viên quốc tế",
    "Jazz band": "Ban nhạc Jazz",
    "Literary magazine": "Tạp chí văn học sinh viên",
    "Marching band": "Đoàn nhạc diễu hành",
    "Model UN": "Mô phỏng LHQ (Model UN)",
    "Music ensembles": "Hòa tấu thính phòng",
    "Musical theater": "Nhạc kịch",
    "Opera": "Nhạc kịch Opera",
    "Pep band": "Ban nhạc cổ động",
    "Radio station": "Đài phát thanh sinh viên",
    "Student government": "Hội đồng sinh viên",
    "Student newspaper": "Báo sinh viên",
    "Student-run film society": "CLB điện ảnh sinh viên",
    "Symphony orchestra": "Dàn nhạc giao hưởng",
    "Television station": "Kênh truyền hình sinh viên",
    "Yearbook": "Ban biên tập kỷ yếu",
  };
  return map[value] ?? value;
}

export function gpaScaleTypeLabel(value: string | null): string {
  if (!value) return MISSING_UNIVERSITY_DATA;
  if (value === "weighted") return "Có tính trọng số (Weighted)";
  if (value === "unweighted") return "Không tính trọng số (Unweighted)";
  if (value === "both") return "Cả hai thang điểm (Weighted & Unweighted)";
  return value;
}

export function schoolSatPolicy(value: string | null): string {
  return ({ required: "Yêu cầu SAT/ACT", optional: "Không bắt buộc SAT/ACT", not_accepted: "Không sử dụng SAT/ACT" } as Record<string, string>)[value ?? ""] ?? MISSING_UNIVERSITY_DATA;
}

export function schoolSatPolicyShort(value: string | null): string | null {
  if (!value) return null;
  const map: Record<string, string> = {
    required: "Bắt buộc",
    optional: "Tùy chọn",
    not_accepted: "Không sử dụng",
  };
  return map[value] ?? null;
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

export function universityDeadlines(value: string | null): { round: string; label: string; date: string; rawDate: string }[] {
  if (!value) return [];
  const labels: Record<string, string> = { ED: "Early Decision", ED2: "Early Decision II", EA: "Early Action", RD: "Regular Decision", Rolling: "Rolling Admission" };
  try {
    const parsed: unknown = JSON.parse(value);
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return [];
    const result: { round: string; label: string; date: string; rawDate: string }[] = [];
    for (const [round, date] of Object.entries(parsed)) {
      if (typeof date === "string" && date.trim()) {
        result.push({
          round,
          label: labels[round] ?? round,
          date: universityDate(date),
          rawDate: date.trim(),
        });
      }
    }
    return result;
  } catch {
    return [];
  }
}

export function deadlineRemainingText(
  targetDateStr: string | null | undefined,
  referenceDate: Date = new Date()
): { isPassed: boolean; label: string } | null {
  if (!targetDateStr) return null;
  const target = new Date(targetDateStr);
  if (isNaN(target.getTime())) return null;

  const today = new Date(referenceDate.getFullYear(), referenceDate.getMonth(), referenceDate.getDate());
  const targetDay = new Date(target.getFullYear(), target.getMonth(), target.getDate());

  const diffMs = targetDay.getTime() - today.getTime();
  const diffDays = Math.round(diffMs / (1000 * 60 * 60 * 24));

  if (diffDays < 0) {
    return { isPassed: true, label: "Đã qua hạn" };
  }
  if (diffDays === 0) {
    return { isPassed: false, label: "Hôm nay là hạn chót" };
  }
  if (diffDays < 60) {
    return { isPassed: false, label: `Còn ${diffDays} ngày` };
  }
  const months = Math.round(diffDays / 30);
  return { isPassed: false, label: `Còn ${months} tháng` };
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
  avg_gpa_weighted: "GPA có trọng số", gpa_scale_type: "Thang điểm GPA", fy_on_campus_pct: "Tỷ lệ SV năm nhất ở KTX", ug_on_campus_pct: "Tỷ lệ SV đại học ở KTX",
  housing_types: "Các loại hình nhà ở", intl_housing: "Khu nhà ở cho SV quốc tế", housing_only_usd: "Tiền phòng ở", housing_deposit_usd: "Tiền cọc nhà ở",
  student_faculty_ratio: "Tỷ lệ SV / giảng viên", activities: "Hoạt động sinh viên", campus_setting: "Bối cảnh khuôn viên",
};
