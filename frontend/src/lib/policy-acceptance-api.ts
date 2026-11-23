import { apiFetch } from "@/lib/api";
import type { AcceptedPolicy, PolicyAcceptanceStatus } from "@/types/policy";

/** [UC-104] Tình trạng đồng ý Điều khoản và Chính sách bảo mật của người đang đăng nhập. */
export const getPolicyAcceptanceStatus = () => apiFetch<PolicyAcceptanceStatus>("/api/v1/policy-acceptance");

/**
 * Ghi đồng ý theo đúng phiên bản đã hiển thị. Server trả 409 (code policy_changed) nếu admin vừa công bố bản khác:
 * khi đó không ghi gì, người dùng phải đọc lại bản mới. Thành công trả tình trạng mới.
 */
export const acceptPolicies = (items: AcceptedPolicy[]) =>
  apiFetch<PolicyAcceptanceStatus>("/api/v1/policy-acceptance", { method: "POST", body: JSON.stringify({ items }) });
