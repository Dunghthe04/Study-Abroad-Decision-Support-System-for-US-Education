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
}

export interface AdminScholarshipFilters {
  universityId?: string;
  search?: string;
  studyLevel?: string;
  active?: "all" | "active" | "hidden";
  page: number;
  pageSize?: number;
}
