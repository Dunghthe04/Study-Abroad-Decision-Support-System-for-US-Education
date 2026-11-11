// Kiểu dữ liệu của API admin: Token và chi phí AI (backend: AdminAiUsageController, StudyAbroad.Application.Admin.AiUsage).
// Ngày là chuỗi "yyyy-MM-dd" theo UTC. Mọi API chỉ trả số đo, không có nội dung chat.

import type { PagedResult } from "@/types/api";

export type { PagedResult };

/** Số đo của một nhóm lần gọi AI. */
export interface AiUsageMetrics {
  calls: number;
  failedCalls: number;
  inputTokens: number;
  outputTokens: number;
  /** Thời gian trung bình của MỘT lần gọi, mili giây. */
  avgLatencyMs: number;
  /** Tổng tiền (USD) của các lần gọi đã có giá. */
  costUsd: number;
  /** Số lần gọi Claude chưa có giá: khác 0 nghĩa là costUsd đang thiếu phần này. */
  unpricedCalls: number;
}

export interface AiUsageByKind {
  kind: string;
  metrics: AiUsageMetrics;
}

export interface AiUsageByPurpose {
  kind: string;
  purpose: string | null;
  metrics: AiUsageMetrics;
}

export interface AiUsageByDay {
  date: string;
  kind: string;
  metrics: AiUsageMetrics;
}

export interface AiUsageOverview {
  from: string;
  to: string;
  total: AiUsageMetrics;
  byKind: AiUsageByKind[];
  byPurpose: AiUsageByPurpose[];
  byDay: AiUsageByDay[];
}

export interface AiUsageUserRank {
  userId: string;
  fullName: string | null;
  email: string | null;
  metrics: AiUsageMetrics;
}

export interface AiUsageUserOverview {
  userId: string;
  fullName: string;
  email: string;
  overview: AiUsageOverview;
}

export interface AiUsageTurnRow {
  messageId: string;
  sessionId: string;
  createdAt: string;
  role: string;
  outcome: string | null;
  metrics: AiUsageMetrics;
}

export interface AiUsageCallDetail {
  id: string;
  createdAt: string;
  kind: string;
  purpose: string | null;
  skillVersion: string | null;
  model: string | null;
  inputTokens: number;
  outputTokens: number;
  latencyMs: number;
  success: boolean;
  error: string | null;
  costUsd: number | null;
}

export interface AiUsageTurnDetail {
  messageId: string;
  sessionId: string;
  userId: string;
  createdAt: string;
  role: string;
  outcome: string | null;
  total: AiUsageMetrics;
  calls: AiUsageCallDetail[];
}

/** Một dòng bảng giá (USD cho một triệu token). Giá đọc từ cấu hình backend (mục AiPricing) nên chỉ đọc. */
export interface AiPrice {
  model: string;
  inputPerMTok: number;
  outputPerMTok: number;
  longInputPerMTok: number;
  longOutputPerMTok: number;
  longPromptThreshold: number;
}

export type AiUsagePage<T> = PagedResult<T>;
