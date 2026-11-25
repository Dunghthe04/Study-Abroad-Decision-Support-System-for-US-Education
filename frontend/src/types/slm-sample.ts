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
  createdAt: string;
  updatedAt: string | null;
  source: string;
  createdByUserId: string | null;
  createdByName: string | null;
  exportedAt: string | null;
  version: number;
  deletedAt: string | null;
}

export interface SlmSampleVersion {
  id: string;
  version: number;
  action: "create" | "edit" | "restore" | "import" | string;
  restoredFromVersion: number | null;
  actorUserId: string | null;
  actorName: string | null;
  createdAt: string;
  sampleType: SlmSampleType;
  question: string;
  profile: string | null;
  pendingQuestion: string | null;
  intents: string;
  context: string | null;
  split: SlmSplit;
  sourceReview: string | null;
  source: string;
}

export interface SlmSampleDetail {
  sample: SlmSample;
  versions: SlmSampleVersion[];
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
}

export interface SlmSampleSummary {
  total: number;
  deleted: number;
  byType: Partial<Record<SlmSampleType, number>>;
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
