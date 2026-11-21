// Khớp các DTO của backend (Application/Admin/AiQuality), JSON camelCase. UC-90.

/** Số lượt theo kết quả. Các nhóm cộng lại đúng bằng total; unclassified là tin cũ chưa có kết quả. */
export interface OutcomeCounts {
  total: number;
  answered: number;
  noAnswer: number;
  refused: number;
  failed: number;
  unclassified: number;
  answeredRate: number;
  noAnswerRate: number;
  refusedRate: number;
  failedRate: number;
  unclassifiedRate: number;
}

/** Thời gian phản hồi cả lượt (ms). Các số là null khi measured bằng 0. */
export interface LatencySummary {
  measured: number;
  avgMs: number | null;
  p50Ms: number | null;
  p95Ms: number | null;
}

export interface AiQualityDay {
  date: string; // yyyy-MM-dd (UTC)
  counts: OutcomeCounts;
  latency: LatencySummary;
}

export interface AiQualityFeature {
  feature: string; // loại ý SLM đầu tiên của lượt; "other" khi không rõ
  counts: OutcomeCounts;
  latency: LatencySummary;
}

export interface FailedStepCount {
  step: string;
  turns: number;
}

export interface AiQualityReport {
  from: string;
  to: string;
  total: OutcomeCounts;
  latency: LatencySummary;
  slmFallbackTurns: number;
  fallbackTurns: number;
  byDay: AiQualityDay[];
  byFeature: AiQualityFeature[];
  failedSteps: FailedStepCount[];
}

/** Ba loại được xem nội dung (nhóm trả lời được dùng chức năng duyệt mẫu). */
export type SampleOutcome = "no_answer" | "refused" | "failed";

export interface AiQualitySampleMessage {
  role: string;
  content: string;
}

/** Một lượt đã che thông tin cá nhân. Không có mã người dùng, mã phiên hay mã tin nhắn. */
export interface AiQualitySample {
  createdAt: string;
  outcome: string;
  feature: string | null;
  turnMs: number | null;
  question: string | null;
  answer: string;
  context: AiQualitySampleMessage[];
}

export interface AiQualitySamplePage {
  items: AiQualitySample[];
  page: number;
  pageSize: number;
  totalCount: number; // số lượt được xem nội dung
  hiddenCount: number; // số lượt cùng loại bị ẩn nội dung vì lý do riêng tư
}
