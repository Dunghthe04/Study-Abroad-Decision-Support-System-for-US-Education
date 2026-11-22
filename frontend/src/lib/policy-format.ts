import type { PolicyKind, PolicyStatus } from "@/types/policy";

export const POLICY_KINDS: { id: PolicyKind; label: string }[] = [
  { id: "terms_of_use", label: "Điều khoản sử dụng" },
  { id: "privacy_policy", label: "Chính sách bảo mật" },
  { id: "child_data_consent", label: "Đồng ý của phụ huynh" },
  { id: "forum_rules", label: "Nội quy diễn đàn" },
];

export const STATUS_LABELS: Record<PolicyStatus, { name: string; variant: "warn" | "ok" | "neutral" }> = {
  draft: { name: "Nháp", variant: "warn" },
  published: { name: "Đang hiệu lực", variant: "ok" },
  retired: { name: "Hết hiệu lực", variant: "neutral" },
};

// Khớp giới hạn của backend (PolicyService)
export const MAX_TITLE = 200;
export const MAX_SUMMARY = 1000;
export const MAX_CONTENT = 200_000;

export function formatDateTime(iso: string | null): string {
  if (!iso) return "—";
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? iso
    : date.toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit", timeZone: "Asia/Ho_Chi_Minh" });
}

/** Kiểm tra trước khi lưu hoặc công bố (backend vẫn kiểm tra lại). Trả câu lỗi hoặc null. */
export function validateDraft(input: { title: string; content: string; changeSummary: string }, forPublish: boolean): string | null {
  if (!input.title.trim()) return "Tiêu đề không được để trống.";
  if (input.title.trim().length > MAX_TITLE) return `Tiêu đề tối đa ${MAX_TITLE} ký tự.`;
  if (input.content.length > MAX_CONTENT) return `Nội dung tối đa ${MAX_CONTENT.toLocaleString("vi-VN")} ký tự.`;
  if (input.changeSummary.trim().length > MAX_SUMMARY) return `Tóm tắt thay đổi tối đa ${MAX_SUMMARY} ký tự.`;
  if (forPublish) {
    if (!input.content.trim()) return "Cần có nội dung trước khi công bố.";
    if (!input.changeSummary.trim()) return "Cần nhập tóm tắt thay đổi trước khi công bố.";
  }
  return null;
}
