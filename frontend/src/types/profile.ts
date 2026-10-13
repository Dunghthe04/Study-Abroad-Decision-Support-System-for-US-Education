// [USAS-364] Định nghĩa các kiểu dữ liệu TypeScript cho Hồ sơ tài chính và Hoạt động ngoại khóa

export interface FinancialProfileDto {
  id: string;
  userId: string;
  annualBudget: number;
  fundingSource: string;
  needScholarship: boolean;
  maxExpectedTuition?: number | null;
  currency: string;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface SaveFinancialProfileRequest {
  annualBudget: number;
  fundingSource: string;
  needScholarship: boolean;
  maxExpectedTuition?: number | null;
  currency?: string;
  notes?: string | null;
}

export interface ExtracurricularActivityDto {
  id: string;
  userId: string;
  activityName: string;
  role: string;
  organization: string;
  durationMonths?: number | null;
  startDate?: string | null;
  endDate?: string | null;
  isOngoing: boolean;
  impactLevel: number;
  description: string;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateExtracurricularRequest {
  activityName: string;
  role: string;
  organization?: string;
  durationMonths?: number | null;
  startDate?: string | null;
  endDate?: string | null;
  isOngoing?: boolean;
  impactLevel: number;
  description?: string;
}

export interface UpdateExtracurricularRequest {
  activityName: string;
  role: string;
  organization?: string;
  durationMonths?: number | null;
  startDate?: string | null;
  endDate?: string | null;
  isOngoing?: boolean;
  impactLevel: number;
  description?: string;
}

export interface StudentAchievementDto {
  id: string;
  userId: string;
  category: string;
  title: string;
  issuer?: string | null;
  issueDate?: string | null;
  description: string;
  createdAt: string;
}

export interface CreateAchievementRequest {
  category: string;
  title: string;
  issuer?: string;
  issueDate?: string;
  description?: string;
}

export interface StudentProfileSummaryDto {
  userId: string;
  financial: FinancialProfileDto | null;
  activities: ExtracurricularActivityDto[];
  achievements: StudentAchievementDto[];
  totalActivitiesCount: number;
  totalAchievementsCount: number;
  maxImpactLevel: number;
}
