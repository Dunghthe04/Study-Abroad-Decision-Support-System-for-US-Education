import type { SlmReviewStatus, SlmSampleType, SlmSplit } from "@/types/slm-sample";

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
