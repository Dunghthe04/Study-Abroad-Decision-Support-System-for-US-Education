import type { PagedResult } from "@/types/api";

export type { PagedResult };

export type SlmSampleType =
  | "collect_profile"
  | "profile_review"
  | "scenario"
  | "knowledge"
  | "center_referral"
  | "out_of_scope";

export type SlmSplit = "train" | "val" | "test";

export type SlmReviewStatus = "draft" | "pending" | "approved" | "rejected" | "needs_fix";

export interface SlmSample {
  id: string;
  sampleType: SlmSampleType;
  question: string;
  profile: string | null;
  pendingQuestion: string | null;
  intents: string;
  context: string | null;
  split: SlmSplit;
  sourceReview: string | null;
  reviewStatus: SlmReviewStatus;
  reviewNote: string | null;
  reviewedByUserId: string | null;
  reviewedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
  source: string;
  createdByUserId: string | null;
  createdByName: string | null;
  exportedAt: string | null;
}

export interface SlmSampleHistoryItem {
  id: string;
  action: "create" | "edit" | "submit" | "review" | "delete" | "capture";
  fromStatus: SlmReviewStatus | null;
  toStatus: SlmReviewStatus | null;
  note: string | null;
  changedFields: string[];
  actorUserId: string | null;
  actorName: string | null;
  createdAt: string;
}

export interface SlmSampleDetail {
  sample: SlmSample;
  reviewedByName: string | null;
  history: SlmSampleHistoryItem[];
}

export interface SlmSampleCreator {
  userId: string;
  name: string;
}

export interface SaveSlmSampleRequest {
  sampleType: SlmSampleType;
  question: string;
  profile: string | null;
  pendingQuestion: string | null;
  intents: string;
  context: string | null;
  split: SlmSplit;
  sourceReview: string | null;
  source: string | null;
  submit?: boolean;
}

export interface SlmSampleSummary {
  total: number;
  byStatus: Partial<Record<SlmReviewStatus, number>>;
  byType: Partial<Record<SlmSampleType, number>>;
}

export interface SlmReviewResult {
  updated: number;
  notFound: number;
  skippedOwn: number;
  skippedNotReady: number;
  skippedNotPending: number;
}

export interface SlmImportError {
  sheetName: string;
  rowNumber: number;
  message: string;
}

export interface SlmImportRow {
  sheetName: string;
  rowNumber: number;
  question: string | null;
  profile: string | null;
  pendingQuestion: string | null;
  intents: string | null;
  context: string | null;
  split: string | null;
  sourceReview: string | null;
}

export interface SlmImportPreview {
  totalRows: number;
  rows: SlmImportRow[];
  errors: SlmImportError[];
}

export interface SlmImportResult {
  totalRows: number;
  inserted: number;
  duplicates: number;
  errors: SlmImportError[];
}
