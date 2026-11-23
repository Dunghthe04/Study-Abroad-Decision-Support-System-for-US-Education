export interface ScholarshipItem {
  id: string;
  universityCode: string | null;
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
  kind: string | null;
  amountType: string | null;
  amountMinUsd: number | null;
  amountMaxUsd: number | null;
  durationYears: number | null;
  renewable: boolean | null;
  vnEligible: boolean | null;
  applicationMethod: string | null;
  applyUrl: string | null;
}

export interface ScholarshipSearchParams {
  search?: string;
  studyLevel?: string;
  minAmountUsd?: number;
  maxAmountUsd?: number;
  coverageType?: string;
  maxMinGpa4?: number;
  maxMinIelts?: number;
  kind?: string;
  vnEligible?: boolean;
  page?: number;
  pageSize?: number;
}
