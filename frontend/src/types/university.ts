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
  avgGpaWeighted: number | null;
  gpaScaleType: string | null;
  fyOnCampusPct: number | null;
  ugOnCampusPct: number | null;
  housingTypes: string | null;
  intlHousing: boolean | null;
  housingOnlyUsd: number | null;
  housingDepositUsd: number | null;
  studentFacultyRatio: number | null;
  activities: string | null;
  campusSetting: string | null;
  actPolicy: string | null;
  actSubmitPct: number | null;
  applicationFeeUsd: number | null;
  tuitionInStateUsd: number | null;
  /** Bảo hiểm y tế bắt buộc / năm; có trường dữ liệu cũ chưa trả về */
  healthInsuranceUsd?: number | null;
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
  /** Ghi công ảnh bìa / ảnh trường theo giấy phép; API cũ chưa trả về */
  imageCredits?: UniversityImageCredit[];
  offerings: UniversityOffering[];
  rankings: UniversityRanking[];
  sources: UniversitySource[];
}

export interface UniversityListItem {
  id: string;
  code: string;
  name: string;
  city: string | null;
  state: string | null;
  stateName: string | null;
  control: string | null;
  acceptanceRate: number | null;
  isIvy: boolean;
  logoUrl: string | null;
  coverUrl: string | null;
  tuitionUsd: number | null;
  totalCostUsd: number | null;
  minIelts: number | null;
  minToefl: number | null;
  minDuolingo: number | null;
  bestRank: number | null;
  bestRankList: string | null;
  satPolicy: string | null;
  actPolicy: string | null;
  intlAidPolicy: string | null;
  majors: string[];
}

export interface UniversityFilterOptionItem {
  value: string;
  label: string;
}

export interface UniversityFilterRanges {
  minLivingUsd: number;
  maxLivingUsd: number;
  minSat: number;
  maxSat: number;
  minAct: number;
  maxAct: number;
  minStudentFacultyRatio: number;
  maxStudentFacultyRatio: number;
  minIntlStudents: number;
  maxIntlStudents: number;
  maxIntlAidAvgUsd: number;
}

export interface UniversityFilterOptions {
  states: UniversityFilterOptionItem[];
  controls: UniversityFilterOptionItem[];
  majors: UniversityFilterOptionItem[];
  minTuitionUsd: number;
  maxTuitionUsd: number;
  minTotalCostUsd: number;
  maxTotalCostUsd: number;
  rankingLists: UniversityFilterOptionItem[];
  ranges: UniversityFilterRanges;
  housingTypes: string[];
  activities: string[];
}

export interface UniversitySearchParams {
  search?: string;
  state?: string;
  region?: string;
  control?: string;
  major?: string;
  minTuitionUsd?: number;
  maxTuitionUsd?: number;
  minTotalCostUsd?: number;
  maxTotalCostUsd?: number;
  minLivingUsd?: number;
  maxLivingUsd?: number;
  maxApplicationFeeUsd?: number;
  maxMinIelts?: number;
  maxMinToefl?: number;
  maxMinDuolingo?: number;
  maxMinGpa4?: number;
  minAvgGpa4?: number;
  maxAvgGpa4?: number;
  minSat?: number;
  maxSat?: number;
  minAct?: number;
  maxAct?: number;
  satPolicy?: string;
  actPolicy?: string;
  deadlineRound?: string;
  c7TestImportance?: string;
  c7EcImportance?: string;
  minAcceptanceRate?: number;
  maxAcceptanceRate?: number;
  minIntlAcceptanceRate?: number;
  maxIntlAcceptanceRate?: number;
  minStudentFacultyRatio?: number;
  maxStudentFacultyRatio?: number;
  minRetentionRate?: number;
  maxRetentionRate?: number;
  minGradRate6Yr?: number;
  maxGradRate6Yr?: number;
  minIntlStudents?: number;
  maxIntlStudents?: number;
  intlAidPolicy?: string;
  minIntlAidAvgUsd?: number;
  maxIntlAidAvgUsd?: number;
  minFyOnCampusPct?: number;
  maxFyOnCampusPct?: number;
  housingType?: string;
  activity?: string;
  isIvy?: boolean;
  testOptional?: boolean;
  hasIntlAid?: boolean;
  hasScholarship?: boolean;
  intlHousing?: boolean;
  campusSetting?: string;
  maxRank?: number;
  sortBy?: string;
  rankListId?: string;
  studyLevel?: string;
  page?: number;
  pageSize?: number;
}


export interface UniversityImageCredit {
  url: string;
  license: string | null;
  author: string | null;
  sourcePageUrl: string | null;
}
