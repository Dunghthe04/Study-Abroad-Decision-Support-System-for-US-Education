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
  minSat?: number | null;
  minAct?: number | null;
  minToefl?: number | null;
  minDuolingo?: number | null;
  meritBased?: boolean | null;
  needBased?: boolean | null;
  majorRestriction?: string | null;
}

export interface ScholarshipSearchParams {
  search?: string;
  minAmountUsd?: number;
  maxAmountUsd?: number;
  coverageType?: string;
  maxMinGpa4?: number;
  maxMinIelts?: number;
  maxMinToefl?: number;
  maxMinDuolingo?: number;
  kind?: string;
  /** Mặc định bật (true) cho du học sinh Việt Nam; người dùng có thể tắt (false) */
  vnEligible?: boolean;
  /** yyyy-MM-dd: chỉ lấy học bổng còn hạn từ ngày này (hạn trống = xét quanh năm vẫn giữ) */
  deadlineFrom?: string;
  /** Chỉ dùng ở URL trang /scholarships: mặc định ẩn học bổng đã hết hạn, true thì hiện cả */
  includeExpired?: boolean;
  page?: number;
  pageSize?: number;
}
