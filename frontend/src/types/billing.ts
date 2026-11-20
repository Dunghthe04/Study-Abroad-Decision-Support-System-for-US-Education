// Gói dịch vụ, đơn hàng và gói Gia đình (docs/billing-impl-spec.md mục 6). Chỉ có phần trăm, không bao giờ có số token.

export type PlanKind = "free" | "personal" | "family";

export interface PlanDto {
  code: string;
  name: string;
  description: string | null;
  kind: PlanKind;
  priceVnd: number;
  durationDays: number | null;
  estimatedQuestions: number;
  seats: { students: number; parents: number } | null;
}

export interface PoolDto {
  planCode: string;
  planName: string;
  startsAt: string | null;
  endsAt: string | null;
  usedPercent: number;
  exhausted: boolean;
  lowQuota: boolean;
  expiringSoon: boolean;
  queued: { planName: string; expectedStartsAt: string | null } | null;
}

export interface FamilyPoolDto extends PoolDto {
  role: "owner" | "member";
  ownerName: string;
}

export interface SubscriptionSummaryDto {
  tier: "free" | "premium";
  chargingPool: PlanKind;
  personal: PoolDto | null;
  family: FamilyPoolDto | null;
  free: { usedPercent: number; resetsAt: string };
  pendingOrderCode: number | null;
  lowQuota: boolean;
  expiringSoon: boolean;
}

export type OrderStatus = "pending" | "paid" | "mismatch" | "expired" | "cancelled";

export interface OrderDto {
  orderCode: number;
  planCode: string;
  planName: string;
  planKind: PlanKind;
  amountVnd: number;
  description: string;
  status: OrderStatus;
  createdAt: string;
  expiresAt: string;
  paidAt: string | null;
  checkoutUrl: string;
  qrCode: string;
  cycle: { startsAt: string | null; endsAt: string | null; queued: boolean } | null;
}

/** Lỗi { code, message } của các route gói/đơn/gia đình; orderCode chỉ có ở pending_order_exists. */
export interface BillingError {
  status: number;
  code: string;
  message: string;
  orderCode: number | null;
}

// ---- Gia đình ----

export type SeatRole = "student" | "parent";

export interface FamilyDto {
  groupId: string;
  isOwner: boolean;
  ownerName: string;
  cycle: { planName: string; endsAt: string | null; usedPercent: number } | null;
  seats: {
    students: { used: number; max: number };
    parents: { used: number; max: number };
  };
  members: {
    memberId: string;
    name: string;
    role: "owner" | SeatRole;
    sharePercent: number;
    joinedAt: string | null;
    isMe: boolean;
  }[];
  invites: { inviteId: string; email: string; seatRole: SeatRole; expiresAt: string; canResendAt: string | null }[];
}

export interface FamilyInviteLookupDto {
  ownerName: string;
  seatRole: SeatRole;
  expiresAt: string;
  status: string;
}
