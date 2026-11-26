// UC-94: Dashboard KPI Types (Student & Parent)

export interface RecommendationKpi {
  reach: number;
  match: number;
  safety: number;
  aid: number;
  total: number;
  runAt: string | null;
  explanation: string;
  hasRun: boolean;
}

export interface ProfileProgressKpi {
  completedSteps: number;
  totalSteps: number;
  hasCommittedProfile: boolean;
  explanation: string;
}

export interface UpcomingDeadlineItem {
  targetSchoolId: string;
  universityId: string;
  universityCode: string;
  universityName: string;
  category: string | null;
  applicationRound: string | null;
  deadline: string | null;
  daysRemaining: number | null;
}

export interface UpcomingDeadlinesKpi {
  upcoming: UpcomingDeadlineItem[];
  undecidedRoundCount: number;
  totalTargetSchools: number;
  explanation: string;
}

export interface DataCompletenessItem {
  key: string;
  label: string;
  isFilled: boolean;
}

export interface DataCompletenessKpi {
  filledCount: number;
  totalCount: number;
  items: DataCompletenessItem[];
  missingLabels: string[];
  explanation: string;
}

export interface StudentDashboard {
  recommendation: RecommendationKpi;
  profileProgress: ProfileProgressKpi;
  upcomingDeadlines: UpcomingDeadlinesKpi;
  dataCompleteness: DataCompletenessKpi;
}

export interface SchoolShortfallItem {
  universityId: string;
  universityCode: string;
  universityName: string;
  totalCost1Year: number | null;
  totalCost4Years: number | null;
  budget4Years: number | null;
  shortfall4Years: number | null;
  hasCostData: boolean;
}

export interface ParentFinancialKpi {
  budgetPerYear: number | null;
  budgetSource: "parent_preference" | "student_profile_tier" | "none";
  budgetSourceLabel: string;
  schoolsWithinBudgetCount: number;
  schoolsTotalWithCostCount: number;
  schoolsMissingCostCount: number;
  totalCost4YearsAllSchools: number;
  shortfallList: SchoolShortfallItem[];
  mostShortfallSchool: SchoolShortfallItem | null;
  leastShortfallSchool: SchoolShortfallItem | null;
  withinBudgetExplanation: string;
  totalCost4YearsExplanation: string;
  shortfallExplanation: string;
}

export interface ParentChildDashboard {
  studentId: string;
  studentName: string;
  isManaged: boolean;
  studentKpis: StudentDashboard;
  financialKpis: ParentFinancialKpi;
}

// ---------- Lộ trình theo trường mục tiêu (GET api/v1/dashboard/roadmap) ----------

export type RoadmapStatus = "met" | "near" | "below" | "unknown";

export interface RoadmapCriterion {
  key: "gpa" | "english" | "sat" | "budget";
  type?: string | null;
  /** của học sinh; budget = ngân sách USD/năm */
  mine?: number | null;
  /** mốc của trường; budget = chi phí USD/năm */
  mark?: number | null;
  status: RoadmapStatus;
}

export interface RoadmapScholarship {
  id: string;
  name: string;
  deadline?: string | null;
  eligible: boolean;
}

export interface RoadmapSchool {
  /** null là dòng "TB các trường" khi chưa có trường mục tiêu */
  universityId?: string | null;
  name: string;
  category?: string | null;
  criteria: RoadmapCriterion[];
  metCount: number;
  knownCount: number;
  scholarships: RoadmapScholarship[];
}

export interface RoadmapMilestone {
  date: string;
  kind: "deadline" | "scholarship" | "english_test" | "sat_test" | "visa" | "intake";
  label: string;
  round?: string | null;
  universityId?: string | null;
  isEstimate: boolean;
}

export interface RoadmapCenter {
  id: string;
  name: string;
  city?: string | null;
  rating: number;
  reviewCount: number;
  verified: boolean;
  feeText?: string | null;
  sameProvince: boolean;
}

export interface RoadmapTask {
  id: string;
  label: string;
  due?: string | null;
  status: "done" | "todo" | "overdue";
  /** false: không có dữ liệu để tự tích, chỉ hiện như mốc, không tính vào tiến độ */
  checkable: boolean;
  schoolIds: string[];
  services: string[];
  centers: RoadmapCenter[];
}

export interface Roadmap {
  mode: "target" | "average";
  intake: string;
  intakeIsEstimate: boolean;
  schools: RoadmapSchool[];
  summary: string;
  milestones: RoadmapMilestone[];
  tasks: RoadmapTask[];
  progress: { done: number; total: number };
  provinceMissing: boolean;
  /** Tỉnh/thành đã lưu, null khi chưa chọn */
  province?: string | null;
}
