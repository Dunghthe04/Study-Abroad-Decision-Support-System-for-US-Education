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
