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
}

export interface AdminUniversityOffering {
  id: string;
  studyLevel: string;
  academicYear: string | null;
  tuitionUsd: number | null;
  livingUsd: number | null;
  feesUsd: number | null;
  otherExpensesUsd: number | null;
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
}

export interface UpdateOfferingRequest {
  academicYear: string | null;
  tuitionUsd: number | null;
  livingUsd: number | null;
  feesUsd: number | null;
  otherExpensesUsd: number | null;
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
  sourceUrl: string | null;
  retrievedAt: string | null;
  sourceReliability: string | null;
  notes: string | null;
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
