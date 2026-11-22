import { apiFetch } from "@/lib/api";
import type {
  PolicyDocument,
  PolicyDocumentSummary,
  PolicyDraftResult,
  PolicyKind,
  UpdatePolicyDraftRequest,
} from "@/types/policy";

const base = "/api/v1/admin/policies";

// Quản lý chính sách và điều khoản (UC-103). Chỉ Administrator. Bản đã công bố không sửa được (409 policy_locked).
export const adminPoliciesApi = {
  /** Lịch sử các phiên bản của một loại: nháp lên đầu, rồi bản đã công bố từ mới đến cũ. Không kèm toàn văn. */
  list: (kind: PolicyKind) => apiFetch<PolicyDocumentSummary[]>(`${base}?kind=${kind}`),

  get: (id: string) => apiFetch<PolicyDocument>(`${base}/${id}`),

  /** Tạo nháp bằng cách chép bản đang hiệu lực; loại đã có nháp thì trả lại nháp đó (created = false). */
  createDraft: (kind: PolicyKind) =>
    apiFetch<PolicyDraftResult>(`${base}/drafts`, { method: "POST", body: JSON.stringify({ kind }) }),

  updateDraft: (id: string, request: UpdatePolicyDraftRequest) =>
    apiFetch<PolicyDocument>(`${base}/${id}`, { method: "PUT", body: JSON.stringify(request) }),

  /** Công bố bản nháp đã lưu: đặt số phiên bản, khóa, bản đang hiệu lực thành hết hiệu lực. */
  publish: (id: string) => apiFetch<PolicyDocument>(`${base}/${id}/publish`, { method: "POST" }),

  discardDraft: (id: string) => apiFetch<null>(`${base}/${id}`, { method: "DELETE" }),
};
