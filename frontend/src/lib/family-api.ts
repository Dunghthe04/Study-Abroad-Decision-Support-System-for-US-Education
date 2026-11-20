// Gói Gia đình (docs/billing-impl-spec.md mục 6). Lỗi { code, message } đọc bằng getBillingError trong billing-api.ts.
// Token lời mời gửi trong body để không lộ ở log URL (như leadParentConfirmApi).

import { apiFetch } from "@/lib/api";
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
