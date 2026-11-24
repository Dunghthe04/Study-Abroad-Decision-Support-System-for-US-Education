export interface AdminScholarship {
  id: string;
  universityId: string | null;
  universityName: string | null;
  name: string;
  provider: string | null;
  studyLevel: string;
  amountUsd: number | null;
  coverageType: string | null;
  minGpa4: number | null;
  minIelts: number | null;
  deadline: string | null;
  eligibilityNotes: string | null;
  sourceUrl: string | null;
  retrievedAt: string | null;
  sourceReliability: string | null;
  isActive: boolean;
  updatedAt: string | null;
  lastEditedBy: string | null;
  kind: string | null;
  amountType: string | null;
  amountMinUsd: number | null;
  amountMaxUsd: number | null;
  durationYears: number | null;
  renewable: boolean | null;
  renewalCriteria: string | null;
  intlEligible: boolean | null;
  vnEligible: boolean | null;
  needBased: boolean | null;
  meritBased: boolean | null;
  minSat: number | null;
  minAct: number | null;
  minToefl: number | null;
  majorRestriction: string | null;
  applicationMethod: string | null;
  applyUrl: string | null;
}

export interface SaveScholarshipRequest {
  universityId: string | null;
  name: string | null;
  provider: string | null;
  studyLevel: string | null;
  amountUsd: number | null;
  coverageType: string | null;
  minGpa4: number | null;
  minIelts: number | null;
  deadline: string | null;
  eligibilityNotes: string | null;
  sourceUrl: string | null;
  retrievedAt: string | null;
  sourceReliability: string | null;
  kind: string | null;
  amountType: string | null;
  amountMinUsd: number | null;
  amountMaxUsd: number | null;
  durationYears: number | null;
  renewable: boolean | null;
  renewalCriteria: string | null;
  intlEligible: boolean | null;
  vnEligible: boolean | null;
  needBased: boolean | null;
  meritBased: boolean | null;
  minSat: number | null;
  minAct: number | null;
  minToefl: number | null;
  majorRestriction: string | null;
  applicationMethod: string | null;
  applyUrl: string | null;
}

export interface AdminScholarshipFilters {
  universityId?: string;
  search?: string;
  studyLevel?: string;
  active?: "all" | "active" | "hidden";
  page: number;
  pageSize?: number;
}
