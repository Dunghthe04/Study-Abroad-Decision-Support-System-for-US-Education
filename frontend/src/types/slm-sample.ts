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

export type SlmReviewStatus = "draft" | "approved" | "rejected" | "needs_fix";

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
  updatedAt: string;
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
}

export interface SlmSampleSummary {
  total: number;
  byStatus: Partial<Record<SlmReviewStatus, number>>;
  byType: Partial<Record<SlmSampleType, number>>;
}

export interface SlmReviewResult {
  updated: number;
  notFound: number;
}

export interface SlmImportError {
  sheetName: string;
  rowNumber: number;
  message: string;
}

export interface SlmImportResult {
  totalRows: number;
  inserted: number;
  duplicates: number;
  errors: SlmImportError[];
}
