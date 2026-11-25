import type { PagedResult } from "@/types/api";

export type { PagedResult };

export type KnowledgeDocType =
  | "faq"
  | "glossary"
  | "proc_overview"
  | "proc_prepare"
  | "proc_steps"
  | "proc_checklist"
  | "proc_mistakes"
  | "timeline"
  | "compare"
  | "situation"
  | "data_table"
  | "resource"
  | "notice";

export type KnowledgeSort = "newest" | "oldest" | "entryNo" | "updated";

export interface KnowledgeEntry {
  id: string;
  entryNo: number;
  docType: KnowledgeDocType;
  label: string;
  question: string;
  answer: string;
  source: string | null;
  sourceUrl: string | null;
  retrievedAt: string;
  topicCode: string | null;
  note: string | null;
  createdAt: string;
  updatedAt: string | null;
  version: number;
  deletedAt: string | null;
  /** Có chữ khi lưu xong nhưng không nhờ được advisor tạo vector (UC-114 4a / UC-115 3a). */
  vectorWarning?: string | null;
}

export interface KnowledgeEntryVersion {
  id: string;
  version: number;
  action: "create" | "edit" | "restore" | "import" | string;
  restoredFromVersion: number | null;
  actorUserId: string | null;
  actorName: string | null;
  createdAt: string;
  docType: KnowledgeDocType;
  label: string;
  question: string;
  answer: string;
  source: string | null;
  sourceUrl: string | null;
  retrievedAt: string;
  topicCode: string | null;
  note: string | null;
}

export interface SaveKnowledgeEntryRequest {
  entryNo: number | null;
  docType: KnowledgeDocType;
  label: string;
  question: string;
  answer: string;
  source: string | null;
  sourceUrl: string | null;
  retrievedAt: string;
  topicCode: string | null;
  note: string | null;
}

export interface KnowledgeImportRow {
  rowNumber: number;
  id: string | null;
  docType: string | null;
  label: string | null;
  question: string | null;
  answer: string | null;
  source: string | null;
  sourceUrl: string | null;
  retrievedAt: string | null;
  topicCode: string | null;
  note: string | null;
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
  created: number;
  updated: number;
  unchanged: number;
  errors: KnowledgeImportError[];
  /** Advisor đã bắt đầu tạo vector cho dòng thiếu vector. */
  vectorsStarted: boolean;
  /** Có chữ khi không nhờ được advisor: dòng đã lưu nhưng chat chưa thấy. */
  vectorWarning: string | null;
}

/** Tiến độ tạo vector. pending: số dòng chat chưa thấy. available=false: không hỏi được advisor. */
export interface KnowledgeEmbedStatus {
  available: boolean;
  pending: number;
  running: boolean;
  done: number;
  error: string | null;
  started: boolean;
}
