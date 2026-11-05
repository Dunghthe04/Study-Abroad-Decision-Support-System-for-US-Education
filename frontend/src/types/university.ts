export interface UniversityMajor {
  name: string;
  cipCode: string | null;
  isStem: boolean;
  isVerified: boolean;
}

export interface UniversityOffering {
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
  applicantsTotal: number | null;
  admittedTotal: number | null;
  intlApplicants: number | null;
  intlAdmitted: number | null;
  intlAcceptanceRate: number | null;
  satPolicy: string | null;
  deadlinesJson: string | null;
  c7GpaImportance: string | null;
  c7TestImportance: string | null;
  c7EcImportance: string | null;
  satSubmitPct: number | null;
  gpaSubmitPct: number | null;
  gpaPct40: number | null;
  gpaPct375To399: number | null;
  gpaPct350To374: number | null;
  gpaPct325To349: number | null;
  gpaPct300To324: number | null;
  gpaPct250To299: number | null;
  gpaPctBelow250: number | null;
  intlAidPolicy: string | null;
  intlAidAvgUsd: number | null;
  intlAidRecipients: number | null;
  retentionRate1Yr: number | null;
  gradRate6Yr: number | null;
  majors: UniversityMajor[];
  sourceUrl: string | null;
  retrievedAt: string | null;
  sourceReliability: string | null;
}

export interface UniversityRanking {
  publisher: string;
  name: string;
  rank: number;
  isTie: boolean;
  rankDisplay: string;
  edition: string | null;
  editionYear: number | null;
  sourceUrl: string | null;
}

export interface UniversitySource {
  fieldName: string;
  fieldValue: string | null;
  sourceUrl: string | null;
  sourceSection: string | null;
  status: string;
  retrievedAt: string | null;
  dataYear: string | null;
  appliedToSnapshot: boolean | null;
  normalization: string | null;
}

export interface UniversityDetail {
  code: string;
  name: string;
  city: string | null;
  state: string | null;
  stateName: string | null;
  website: string | null;
  control: string | null;
  acceptanceRate: number | null;
  internationalStudentCount: number | null;
  isIvy: boolean;
  logoUrl: string | null;
  coverUrl: string | null;
  galleryUrls: string[];
  offerings: UniversityOffering[];
  rankings: UniversityRanking[];
  sources: UniversitySource[];
}
