export interface AdminUniversityListItem {
  id: string;
  code: string;
  name: string;
  city: string | null;
  state: string | null;
  control: string | null;
  acceptanceRate: number | null;
  isActive: boolean;
  studyLevels: string[];
  latestRetrievedAt: string | null;
  logoUrl?: string | null;
  scholarshipCount?: number;
}

export interface AdminUniversityOffering {
  id: string;
  studyLevel: string;
  academicYear: string | null;
  tuitionUsd: number | null;
  livingUsd: number | null;
  feesUsd: number | null;
  otherExpensesUsd: number | null;
  healthInsuranceUsd: number | null;
  minGpa4: number | null;
  minIelts: number | null;
  minToefl: number | null;
  minDuolingo: number | null;
  avgGpa4: number | null;
  sat25: number | null;
  sat75: number | null;
  act25: number | null;
  act75: number | null;
  satPolicy: string | null;
  majors: string[];
  stemMajors: string[];
  majorItems: AdminOfferingMajor[];
  deadlinesJson: string | null;
  notes: string | null;
  sourceUrl: string | null;
  retrievedAt: string | null;
  sourceReliability: string | null;
  updatedAt: string | null;
  lastEditedBy: string | null;
  applicationFeeUsd: number | null;
  tuitionInStateUsd: number | null;
  actPolicy: string | null;
  actSubmitPct: number | null;
  applicantsTotal: number | null;
  admittedTotal: number | null;
  intlApplicants: number | null;
  intlAdmitted: number | null;
  intlAcceptanceRate: number | null;
  satSubmitPct: number | null;
  gpaSubmitPct: number | null;
  gpaPct40: number | null;
  gpaPct375To399: number | null;
  gpaPct350To374: number | null;
  gpaPct325To349: number | null;
  gpaPct300To324: number | null;
  gpaPct250To299: number | null;
  gpaPctBelow250: number | null;
  avgGpaWeighted: number | null;
  retentionRate1Yr: number | null;
  gradRate6Yr: number | null;
  studentFacultyRatio: number | null;
  intlAidAvgUsd: number | null;
  intlAidRecipients: number | null;
  fyOnCampusPct: number | null;
  ugOnCampusPct: number | null;
  housingOnlyUsd: number | null;
  housingDepositUsd: number | null;
  gpaScaleType: string | null;
  c7GpaImportance: string | null;
  c7TestImportance: string | null;
  c7EcImportance: string | null;
  intlAidPolicy: string | null;
  campusSetting: string | null;
  housingTypes: string | null;
  activities: string | null;
  intlHousing: boolean | null;
}

export interface AdminOfferingMajor {
  majorId: string;
  name: string;
  cipCode: string | null;
  isStem: boolean;
  isVerified: boolean;
}

export interface AdminMajorOption {
  id: string;
  name: string;
  cipCode: string | null;
}

export interface AdminUniversityDetail {
  id: string;
  code: string;
  name: string;
  city: string | null;
  state: string | null;
  website: string | null;
  control: string | null;
  acceptanceRate: number | null;
  internationalStudentCount: number | null;
  isIvy: boolean;
  isActive: boolean;
  updatedAt: string | null;
  lastEditedBy: string | null;
  logoUrl?: string | null;
  coverUrl?: string | null;
  galleryUrls?: string[] | null;
}

export interface CreateUniversityRequest {
  code: string;
  name: string;
  city: string | null;
  state: string | null;
  website: string | null;
  control: string | null;
  acceptanceRate: number | null;
  internationalStudentCount: number | null;
  isIvy: boolean;
  logoUrl?: string | null;
  coverUrl?: string | null;
  confirmDuplicateName?: boolean;
}

export interface UpdateUniversityRequest {
  name: string;
  city: string | null;
  state: string | null;
  website: string | null;
  control: string | null;
  acceptanceRate: number | null;
  internationalStudentCount: number | null;
  isIvy: boolean;
  logoUrl?: string | null;
  coverUrl?: string | null;
  confirmDuplicateName?: boolean;
}

export interface UpdateOfferingRequest {
  academicYear: string | null;
  tuitionUsd: number | null;
  livingUsd: number | null;
  feesUsd: number | null;
  otherExpensesUsd: number | null;
  healthInsuranceUsd: number | null;
  applicationFeeUsd: number | null;
  tuitionInStateUsd: number | null;
  minGpa4: number | null;
  minIelts: number | null;
  minToefl: number | null;
  minDuolingo: number | null;
  avgGpa4: number | null;
  sat25: number | null;
  sat75: number | null;
  act25: number | null;
  act75: number | null;
  satPolicy: string | null;
  actPolicy: string | null;
  actSubmitPct: number | null;
  sourceUrl: string | null;
  retrievedAt: string | null;
  sourceReliability: string | null;
  notes: string | null;
  applicantsTotal: number | null;
  admittedTotal: number | null;
  intlApplicants: number | null;
  intlAdmitted: number | null;
  intlAcceptanceRate: number | null;
  satSubmitPct: number | null;
  gpaSubmitPct: number | null;
  gpaPct40: number | null;
  gpaPct375To399: number | null;
  gpaPct350To374: number | null;
  gpaPct325To349: number | null;
  gpaPct300To324: number | null;
  gpaPct250To299: number | null;
  gpaPctBelow250: number | null;
  avgGpaWeighted: number | null;
  retentionRate1Yr: number | null;
  gradRate6Yr: number | null;
  studentFacultyRatio: number | null;
  intlAidAvgUsd: number | null;
  intlAidRecipients: number | null;
  fyOnCampusPct: number | null;
  ugOnCampusPct: number | null;
  housingOnlyUsd: number | null;
  housingDepositUsd: number | null;
  gpaScaleType: string | null;
  c7GpaImportance: string | null;
  c7TestImportance: string | null;
  c7EcImportance: string | null;
  intlAidPolicy: string | null;
  campusSetting: string | null;
  housingTypes: string | null;
  activities: string | null;
  intlHousing: boolean | null;
  deadlinesJson: string | null;
  majors: { majorId: string; isStem: boolean }[];
}

/** Lỗi theo từng ô của form, khóa là tên trường dữ liệu (ví dụ "sat25"). */
export type AdminFieldErrors = Record<string, string>;

export type AdminUniversityActiveFilter = "all" | "active" | "hidden";

export interface AdminUniversityFilters {
  search: string;
  state: string;
  control: string;
  active: AdminUniversityActiveFilter;
  page: number;
}
