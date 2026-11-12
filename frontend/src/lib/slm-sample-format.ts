import type { LabeledOption } from "@/lib/admin-university-display";
import type { SaveSlmSampleRequest, SlmReviewStatus, SlmSample, SlmSampleType, SlmSplit } from "@/types/slm-sample";

export const SAMPLE_TYPES: { id: SlmSampleType; label: string }[] = [
  { id: "collect_profile", label: "Thu thập hồ sơ" },
  { id: "profile_review", label: "Đánh giá hồ sơ" },
  { id: "scenario", label: "Kịch bản" },
  { id: "knowledge", label: "Tri thức" },
  { id: "center_referral", label: "Giới thiệu trung tâm" },
  { id: "out_of_scope", label: "Ngoài phạm vi" },
];

export const SPLITS: { id: SlmSplit; label: string }[] = [
  { id: "train", label: "Train" },
  { id: "val", label: "Val" },
  { id: "test", label: "Test" },
];

export const REVIEW_STATUSES: { id: SlmReviewStatus; label: string }[] = [
  { id: "draft", label: "Nháp" },
  { id: "approved", label: "Đã duyệt" },
  { id: "rejected", label: "Từ chối" },
  { id: "needs_fix", label: "Cần sửa" },
];

export const SAMPLE_TYPE_OPTIONS: LabeledOption[] = SAMPLE_TYPES.map((item) => ({ value: item.id, label: item.label }));
export const SPLIT_OPTIONS: LabeledOption[] = SPLITS.map((item) => ({ value: item.id, label: item.label }));

export const REVIEW_STATUS_OPTIONS: LabeledOption[] = REVIEW_STATUSES.map((item) => ({ value: item.id, label: item.label }));

export function sampleTypeLabel(type: SlmSampleType): string {
  return SAMPLE_TYPES.find((item) => item.id === type)?.label ?? type;
}

export function statusLabel(status: SlmReviewStatus): string {
  return REVIEW_STATUSES.find((item) => item.id === status)?.label ?? status;
}

export function statusBadgeVariant(status: SlmReviewStatus): "neutral" | "ok" | "risk" | "warn" {
  if (status === "approved") return "ok";
  if (status === "rejected") return "risk";
  if (status === "needs_fix") return "warn";
  return "neutral";
}

export function jsonProblem(text: string): string | null {
  if (text.trim() === "") return "Không được để trống.";
  try {
    JSON.parse(text);
    return null;
  } catch {
    return "Không phải JSON hợp lệ.";
  }
}

export function optionalJsonProblem(text: string): string | null {
  if (text.trim() === "") return null;
  return jsonProblem(text);
}

export function jsonArrayProblem(text: string): string | null {
  const problem = jsonProblem(text);
  if (problem) return problem;
  return Array.isArray(JSON.parse(text)) ? null : "Phải là một mảng JSON.";
}

export function prettyJson(text: string | null): string {
  if (text === null || text.trim() === "") return "";
  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
}

export function formatDateTime(iso: string | null): string {
  return iso ? new Date(iso).toLocaleString("vi-VN") : "—";
}

export function sampleToRequest(sample: SlmSample): SaveSlmSampleRequest {
  return {
    sampleType: sample.sampleType,
    question: sample.question,
    profile: sample.profile,
    pendingQuestion: sample.pendingQuestion,
    intents: sample.intents,
    context: sample.context,
    split: sample.split,
    sourceReview: sample.sourceReview,
  };
}
