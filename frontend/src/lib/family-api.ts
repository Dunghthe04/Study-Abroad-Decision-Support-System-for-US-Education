// Gói Gia đình (docs/billing-impl-spec.md mục 6). Lỗi { code, message } đọc bằng getBillingError trong billing-api.ts.
// Token lời mời gửi trong body để không lộ ở log URL (như leadParentConfirmApi).

import { apiFetch } from "@/lib/api";
import { getBillingError } from "@/lib/billing-api";
import type { FamilyDto, FamilyInviteLookupDto, SeatRole } from "@/types/billing";

const BASE = "/api/v1/family";
const post = <T>(path: string, body?: unknown) =>
  apiFetch<T>(`${BASE}${path}`, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });

export const familyApi = {
  /** 404 not_in_family. */
  get: () => apiFetch<FamilyDto>(BASE),

  /** 202. Lỗi: no_family, not_owner, invalid_email, seat_full, invite_exists. */
  invite: (email: string, seatRole: SeatRole) => post<{ message: string }>("/invites", { email, seatRole }),

  /** 202. Lỗi: resend_too_soon (429), not_pending. */
  resendInvite: (inviteId: string) => post<{ message: string }>(`/invites/${encodeURIComponent(inviteId)}/resend`),

  /** 204. Lỗi: not_pending. */
  cancelInvite: (inviteId: string) =>
    apiFetch<void>(`${BASE}/invites/${encodeURIComponent(inviteId)}`, { method: "DELETE" }),

  /** Không cần đăng nhập. 404 invite_invalid. */
  lookupInvite: (token: string) => post<FamilyInviteLookupDto>("/invites/lookup", { token }),

  /** Lỗi: invite_invalid, invite_expired, not_pending, wrong_account, role_mismatch, seat_full, already_in_family, pending_family_order. */
  acceptInvite: (token: string) => post<FamilyDto>("/invites/accept", { token }),

  declineInvite: (token: string) => post<void>("/invites/decline", { token }),

  /** 204. Lỗi: not_owner, cannot_remove_owner. */
  removeMember: (memberId: string) =>
    apiFetch<void>(`${BASE}/members/${encodeURIComponent(memberId)}`, { method: "DELETE" }),

  /** 204. Lỗi: owner_cannot_leave, not_in_family. */
  leave: () => post<void>("/leave"),
};

const FAMILY_ERROR_MESSAGES: Record<string, string> = {
  no_family: "Bạn chưa có gói Gia đình đang dùng.",
  not_owner: "Chỉ chủ gói mới làm được thao tác này.",
  invalid_email: "Email không hợp lệ hoặc là email của chính bạn.",
  seat_full: "Loại chỗ này đã đủ người (tính cả lời mời đang chờ).",
  invite_exists: "Email này đã có lời mời đang chờ. Hãy dùng nút Gửi lại.",
  resend_too_soon: "Bạn vừa gửi lại lời mời. Đợi một lúc rồi thử lại nhé.",
  not_pending: "Lời mời này không còn ở trạng thái chờ.",
  invite_invalid: "Link lời mời không hợp lệ hoặc đã bị hủy.",
  invite_expired: "Lời mời đã hết hạn. Hãy nhờ chủ gói gửi lại lời mời.",
  wrong_account: "Bạn đang đăng nhập bằng tài khoản khác với email được mời",
  role_mismatch: "Loại tài khoản của bạn không khớp với chỗ được mời. Hãy nhờ chủ gói hủy và mời lại đúng loại.",
  already_in_family: "Bạn đang ở trong một gói Gia đình khác còn hạn (là chủ gói hoặc thành viên). Bạn có thể tham gia khi gói đó kết thúc.",
  pending_family_order: "Bạn có đơn mua gói Gia đình chưa thanh toán. Hãy hủy đơn hoặc đợi đơn hết hạn rồi chấp nhận lại.",
  cannot_remove_owner: "Không thể xóa chủ gói khỏi gói.",
  owner_cannot_leave: "Chủ gói không thể rời gói của mình.",
  not_in_family: "Bạn không còn trong gói Gia đình này.",
};

/** Câu tiếng Việt cho mọi mã lỗi gói Gia đình (billing-impl-spec mục 6). */
export function familyErrorMessage(error: unknown): string {
  const e = getBillingError(error);
  if (e) return FAMILY_ERROR_MESSAGES[e.code] ?? e.message;
  return error instanceof Error ? error.message : "Đã có lỗi xảy ra. Bạn thử lại nhé.";
}
