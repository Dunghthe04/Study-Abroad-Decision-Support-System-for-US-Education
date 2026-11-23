import { apiFetch } from "@/lib/api";
import type { AcceptedPolicy, PolicyAcceptanceHistory, PolicyAcceptanceStatus } from "@/types/policy";

/** [UC-104] Tình trạng đồng ý Điều khoản và Chính sách bảo mật của người đang đăng nhập. */
export const getPolicyAcceptanceStatus = () => apiFetch<PolicyAcceptanceStatus>("/api/v1/policy-acceptance");

/** Lịch sử các lần mình đồng ý (hoặc từ chối) Điều khoản và Chính sách bảo mật, mới nhất trước. Chỉ của chính mình. */
export const getPolicyAcceptanceHistory = () => apiFetch<PolicyAcceptanceHistory>("/api/v1/policy-acceptance/history");

/**
 * Ghi đồng ý theo đúng phiên bản đã hiển thị. Server trả 409 (code policy_changed) nếu admin vừa công bố bản khác:
 * khi đó không ghi gì, người dùng phải đọc lại bản mới. Thành công trả tình trạng mới.
 */
export const acceptPolicies = (items: AcceptedPolicy[]) =>
  apiFetch<PolicyAcceptanceStatus>("/api/v1/policy-acceptance", { method: "POST", body: JSON.stringify({ items }) });
