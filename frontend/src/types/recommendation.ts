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
}

export interface StudentSummary {
  major: string | null;
  gpa4: number | null; // thang 4
  sat: number | null;
  ielts: number | null;
  toefl: number | null;
  duolingo: number | null;
  annualBudgetUsd: number | null;
  extracurricularScore: number | null; // 0–4
}

export interface ExtracurricularSummary {
  score: number | null; // 0–4
  fresh: boolean;
  aiUsed: boolean;
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
