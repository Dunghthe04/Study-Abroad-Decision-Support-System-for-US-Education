// Gói dịch vụ và đơn hàng (docs/billing-impl-spec.md mục 6). Lỗi đọc bằng getBillingError.

import { apiFetch, ApiError } from "@/lib/api";
import type { PagedResult } from "@/types/api";
import type { BillingError, ExpiredCycleDto, QuotaOverviewDto, OrderDto, OrderStatus, PlanDto, SubscriptionSummaryDto } from "@/types/billing";

export const billingApi = {
  /** Không cần đăng nhập. Đã sắp theo displayOrder, chỉ gói đang bán. */
  plans: () => apiFetch<PlanDto[]>("/api/v1/plans"),

  /** Chỉ học sinh, phụ huynh. */
  subscription: () => apiFetch<SubscriptionSummaryDto>("/api/v1/subscription"),

  /** Hạn mức chung còn lại của tôi ngay lúc này, một con số phần trăm. Chỉ học sinh, phụ huynh. */
  quotaOverview: () => apiFetch<QuotaOverviewDto>("/api/v1/subscription/quota"),

  /** Các kỳ đã hết hạn của tôi, hết hạn gần nhất trước (tối đa 100). */
  expiredCycles: () => apiFetch<ExpiredCycleDto[]>("/api/v1/subscription/expired"),

  /** 201. Lỗi: age_restricted, role_not_allowed, plan_not_for_sale, pending_order_exists (+orderCode), already_in_family, gateway_unavailable. */
  createOrder: (planCode: string) =>
    apiFetch<OrderDto>("/api/v1/orders", { method: "POST", body: JSON.stringify({ planCode }) }),

  getOrder: (orderCode: number | string) => apiFetch<OrderDto>(`/api/v1/orders/${encodeURIComponent(orderCode)}`),

  listOrders: (page = 1, pageSize = 20) =>
    apiFetch<PagedResult<OrderDto>>(`/api/v1/orders?page=${page}&pageSize=${pageSize}`),

  /** Lỗi: 409 not_pending. */
  cancelOrder: (orderCode: number | string) =>
    apiFetch<OrderDto>(`/api/v1/orders/${encodeURIComponent(orderCode)}/cancel`, { method: "POST" }),
};

/** Đọc { code, message, orderCode? } từ ApiError; null nếu lỗi không có code (lỗi mạng, ProblemDetails thường...). */
export function getBillingError(error: unknown): BillingError | null {
  if (!(error instanceof ApiError)) return null;
  const body = error.body as { code?: unknown; message?: unknown; orderCode?: unknown } | null;
  if (!body || typeof body.code !== "string") return null;
  return {
    status: error.status,
    code: body.code,
    message: typeof body.message === "string" ? body.message : error.message,
    orderCode: typeof body.orderCode === "number" ? body.orderCode : null,
  };
}

/** 403 premium_required của gợi ý trường / mô phỏng. Phải xem code: age_restricted cũng là 403. */
export function isPremiumRequired(error: unknown): boolean {
  return getBillingError(error)?.code === "premium_required";
}

export const formatVnd = (amount: number) => `${amount.toLocaleString("vi-VN")}đ`;

export const formatMultiplier = (n: number): string =>
  Number(n.toFixed(1)).toLocaleString("vi-VN", { maximumFractionDigits: 1 });

/** Ngày theo giờ Việt Nam (server gửi +07:00); không ép múi giờ thì ở nước ngoài resetsAt hiện lùi một ngày. */
export const formatVnDate = (iso: string | null | undefined) =>
  iso ? new Date(iso).toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "";

export const formatVnDateTime = (iso: string | null | undefined) =>
  iso ? new Date(iso).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "";

export const ORDER_STATUS_LABEL: Record<OrderStatus, string> = {
  pending: "Chờ thanh toán",
  paid: "Đã thanh toán",
  mismatch: "Sai số tiền",
  expired: "Hết hạn",
  cancelled: "Đã hủy",
};

export const ORDER_STATUS_BADGE = {
  pending: "brand",
  paid: "ok",
  mismatch: "warn",
  expired: "neutral",
  cancelled: "neutral",
} as const satisfies Record<OrderStatus, string>;
