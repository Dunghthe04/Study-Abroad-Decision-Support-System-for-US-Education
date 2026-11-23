// [UC-85] Phân tích độ nhạy tham số thuật toán gợi ý trường. Khớp DTO backend (StudyAbroad.Application.Admin.Sensitivity,
// StudyAbroad.Application.Recommendations.Sensitivity).

/** grouping: tham số xếp nhóm, đo % cặp (hồ sơ, trường) đổi nhóm. fit_weight: trọng số điểm phù hợp, đo % chỗ top 4 đổi trường. */
export type SensitivityKind = "grouping" | "fit_weight";

/** stable: Ổn định · watch: Cần theo dõi · sensitive: Nhạy */
export type SensitivityLabel = "stable" | "watch" | "sensitive";

export interface SensitivityParameterInfo {
  key: string;
  name: string;
  kind: SensitivityKind;
  /** Giá trị đang chạy thật. */
  current: number;
}

export interface SensitivityRequest {
  parameterKeys: string[];
  /** 0.1 và/hoặc 0.2; mỗi mức chạy cả chiều cộng và chiều trừ. */
  steps: number[];
  /** Bỏ trống: 0.02. */
  stableBelow?: number;
  /** Bỏ trống: 0.10. */
  watchUpTo?: number;
}

/** Một mức nhích của một tham số. */
export interface SensitivityPoint {
  /** -0.2, -0.1, 0.1, 0.2 */
  step: number;
  /** Giá trị thật đã dùng (sau khi kẹp vào 0–1). */
  value: number;
  clamped: boolean;
  pairs: number;
  changedPairs: number;
  /** Số cặp chuyển sang nhóm dễ hơn (chiều nguy hiểm). */
  movedSafer: number;
  movedHarder: number;
  slots: number;
  changedSlots: number;
  groupChangeRate: number;
  topChangeRate: number;
  topOverlap: number;
  /** Số phụ (không dùng gắn nhãn), xét toàn bộ thứ tự trong ba nhóm chính như danh sách "Xem thêm". Null ở lần chạy cũ. */
  rankedSchools?: number | null;
  rankShiftTotal?: number | null;
  /** Trung bình mỗi trường dịch bao nhiêu bậc trong nhóm. */
  avgRankShift?: number | null;
}

export interface SensitivityParameterResult {
  key: string;
  name: string;
  kind: SensitivityKind;
  baseline: number;
  points: SensitivityPoint[];
  /** Số dùng gắn nhãn (lớn nhất của ±10%); null khi không chạy mức 10%. */
  rate: number | null;
  label: SensitivityLabel | null;
}

export interface SensitivityReport {
  algorithmVersion: string;
  profileSetVersion: string;
  profileSetHash: string;
  profileCount: number;
  schoolCount: number;
  schoolDataHash: string;
  stableBelow: number;
  watchUpTo: number;
  parameters: SensitivityParameterResult[];
}

export interface SensitivityRunListRow {
  id: string;
  createdAt: string;
  createdByUserId: string | null;
  createdByName: string | null;
  algorithmVersion: string;
  profileSetVersion: string;
  profileSetHash: string;
  profileCount: number;
  schoolCount: number;
  schoolDataHash: string;
  parameterKeys: string[];
  steps: number[];
  stableCount: number;
  watchCount: number;
  sensitiveCount: number;
  durationMs: number;
}

export interface SensitivityRunDetail {
  summary: SensitivityRunListRow;
  request: Required<SensitivityRequest>;
  /** Giá trị mọi tham số lúc chạy (mốc để nhích). */
  parameters: Record<string, number>;
  report: SensitivityReport;
}

export type SensitivityComparisonWarning =
  | "algorithm_version_differs"
  | "profile_set_differs"
  | "school_data_differs"
  | "thresholds_differ";

export interface SensitivityStepComparison {
  step: number;
  baseRate: number | null;
  otherRate: number | null;
  delta: number | null;
}

export interface SensitivityParameterComparison {
  key: string;
  name: string;
  kind: SensitivityKind;
  baseBaseline: number | null;
  otherBaseline: number | null;
  baseRate: number | null;
  otherRate: number | null;
  baseLabel: SensitivityLabel | null;
  otherLabel: SensitivityLabel | null;
  steps: SensitivityStepComparison[];
  /** Sau trừ trước; null khi một bên không có. */
  delta: number | null;
  baselineChanged: boolean;
}

/** base: lần chạy trước · other: lần chạy sau (null khi chưa có lần trước). */
export interface SensitivityComparison {
  base: SensitivityRunListRow;
  other: SensitivityRunListRow | null;
  warnings: SensitivityComparisonWarning[];
  parameters: SensitivityParameterComparison[];
  hasEarlierRun: boolean;
}
