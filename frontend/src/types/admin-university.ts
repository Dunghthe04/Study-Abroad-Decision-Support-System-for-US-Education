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
  satPolicy: string | null;
  majors: string[];
  stemMajors: string[];
  deadlinesJson: string | null;
  notes: string | null;
  sourceUrl: string | null;
  retrievedAt: string | null;
}

export type AdminUniversityActiveFilter = "all" | "active" | "hidden";

export interface AdminUniversityFilters {
  search: string;
  state: string;
  control: string;
  active: AdminUniversityActiveFilter;
  page: number;
}
