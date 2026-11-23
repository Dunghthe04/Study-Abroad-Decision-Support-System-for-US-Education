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
  imageUrl: string | null;
  note: string | null;
  createdAt: string;
  updatedAt: string | null;
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
  imageUrl: string | null;
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
  imageUrl: string | null;
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
  /** Dòng có note bắt đầu bằng "CHỜ DUYỆT": bị bỏ qua, không vào kho. */
  pending: number;
}

export interface KnowledgeImportResult {
  totalRows: number;
  created: number;
  updated: number;
  unchanged: number;
  errors: KnowledgeImportError[];
  pending: number;
}
