// Gợi ý trường: kiểu dữ liệu theo docs/API_GOI_Y_TRUONG.md

export type Category = "reach" | "match" | "safety" | "insufficient_data";
export type EnglishStatus = "met" | "below_min" | "no_score" | "unknown";

export interface SchoolInfo {
  city: string | null;
  state: string | null;
  control: string | null; // public | private
  website: string | null;
  acceptanceRate: number | null;
  internationalStudents: number | null;
  satPolicy: string | null;
  tuitionUsd: number | null;
  livingUsd: number | null;
  feesUsd: number | null;
  minIelts: number | null;
  minToefl: number | null;
  minDuolingo: number | null;
  avgGpa4?: number | null; // GPA trung bình sinh viên trúng tuyển (thang 4)
  sat25?: number | null;
  sat75?: number | null;
}

export interface RecommendationItem {
  rank: number;
  universityId: string;
  offeringId: string;
  code: string;
  name: string;
  state: string | null;
  category: Category;
  score: number | null; // điểm xếp hạng trong nhóm, không hiển thị
  totalCostUsd: number | null;
  costUnknown: boolean;
  english: EnglishStatus;
  openAdmission: boolean;
  reason: string;
  aiExplained: boolean;
  school?: SchoolInfo;
  categoryReason?: string | null; // vì sao trường thuộc nhóm này (code dựng từ số liệu); kết quả cũ không có
  strengths?: string[] | null;
  weaknesses?: string[] | null;
  // Công thức mới (backend chia khối): kết quả cũ đã lưu có thể không có các trường này
  block?: RecommendationBlock | null; // khối trên màn 9, backend quyết định
  farReach?: boolean; // Khó vào xa: đã Khó vào mà trường còn nhận ít du học sinh
  rankScore?: number | null; // X 0–1 theo nút Cân bằng
  why?: string[] | null; // mã lý do: GPA_LOW, SAT_IN_RANGE, NEED_AID...
  flags?: string[] | null; // cờ dữ liệu: COST_UNKNOWN, C7_DEFAULT...
  overBudgetUsd?: number | null; // số tiền vượt ngân sách mỗi năm (khối aid)
  costFit?: number | null; // F 0–1
  aidFit?: number | null; // H 0–1
  quality?: number | null; // Q 0–1
  gpaPosition?: number | null; // GPA cao hơn bao nhiêu % sinh viên trường (0–100)
  satPosition?: number | null; // vị trí SAT trong trường (25 = mốc 25%, 75 = mốc 75%)
  ecAdjustment?: number | null; // cộng/trừ do ngoại khóa ở trường này (−6..+6)
}

/** Lời tư vấn AI viết cho 1 trường (gọi khi mở "Chi tiết"). */
export interface SchoolAdvice {
  summary: string;
  strengths: string[];
  improvements: string[];
  model: string;
}

export type RecommendationBlock = "reach" | "match" | "safety" | "aid" | "other";

export interface StudentSummary {
  major: string | null;
  gpa4: number | null; // thang 4
  sat: number | null;
  ielts: number | null;
  toefl: number | null;
  duolingo: number | null;
  annualBudgetUsd?: number | null;
  budgetMinUsd?: number | null;
  budgetMaxUsd?: number | null;
  extracurricularScore?: number | null; // 0–4
  activitiesCount?: number;
}

export interface ExtracurricularSummary {
  score: number | null; // 0–4
  fresh: boolean;
  aiUsed: boolean;
  activities?: { id: string; name: string }[]; // các hoạt động đã chấm (BE trả thêm vai trò, điểm…)
}

export interface RecommendationResult {
  id: string;
  createdAt: string; // ISO, giờ UTC
  studyLevel: string;
  items: RecommendationItem[];
  warnings: string[];
  extracurricular?: ExtracurricularSummary;
  student?: StudentSummary | null; // hồ sơ dùng cho lần lọc này; kết quả cũ không có
}
