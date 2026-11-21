import type { PagedResult } from "@/types/api";
import type { SlmReviewStatus } from "@/types/slm-sample";

export type { PagedResult };

/** Cùng 5 trạng thái với mẫu SLM, nên dùng lại kiểu và SlmStatusBadge. */
export type KnowledgeReviewStatus = SlmReviewStatus;

export type KnowledgeLevel = "chung" | "thcs" | "thpt" | "dai_hoc" | "thac_si" | "tien_si";

export type KnowledgeOrigin = "manual" | "excel" | "chat-unanswered";

export type KnowledgeSort = "newest" | "oldest" | "entryNo" | "updated";

export interface KnowledgeEntry {
  id: string;
  entryNo: number;
  label: string | null;
  levels: KnowledgeLevel[];
  question: string;
  answer: string | null;
  source: string | null;
  origin: KnowledgeOrigin;
  reviewStatus: KnowledgeReviewStatus;
  reviewNote: string | null;
  reviewedByUserId: string | null;
  reviewedAt: string | null;
  createdByUserId: string | null;
  createdByName: string | null;
  createdAt: string;
  updatedAt: string | null;
  deletedAt: string | null;
}

export interface KnowledgeHistoryItem {
  id: string;
  action: "create" | "edit" | "submit" | "review" | "delete" | "restore" | "capture" | "migrate";
  fromStatus: KnowledgeReviewStatus | null;
  toStatus: KnowledgeReviewStatus | null;
  note: string | null;
  changedFields: string[];
  actorUserId: string | null;
  actorName: string | null;
  createdAt: string;
}

export interface KnowledgeEntryDetail {
  entry: KnowledgeEntry;
  reviewedByName: string | null;
  history: KnowledgeHistoryItem[];
}

export interface KnowledgeCreator {
  userId: string;
  name: string;
}

export interface SaveKnowledgeEntryRequest {
  label: string | null;
  levels: KnowledgeLevel[];
  question: string;
  answer: string | null;
  source: string | null;
  submit?: boolean;
}

export interface KnowledgeReviewResult {
  updated: number;
  notFound: number;
  skippedOwn: number;
  skippedNotReady: number;
  skippedNotPending: number;
}

export interface KnowledgeImportRow {
  rowNumber: number;
  id: string | null;
  label: string | null;
  level: string | null;
  question: string | null;
  answer: string | null;
  source: string | null;
}

export interface KnowledgeImportError {
  rowNumber: number;
  message: string;
}

export interface KnowledgeImportPreview {
  totalRows: number;
  rows: KnowledgeImportRow[];
  errors: KnowledgeImportError[];
}

export interface KnowledgeImportResult {
  totalRows: number;
  inserted: number;
  duplicates: number;
  errors: KnowledgeImportError[];
}