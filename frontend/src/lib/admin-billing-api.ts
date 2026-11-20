// Quản trị gói và đơn hàng (docs/billing-impl-spec.md mục 7). Lỗi dạng { code, message } đọc bằng getBillingError.

import { apiFetch } from "@/lib/api";
import { getBillingError } from "@/lib/billing-api";
import type { PagedResult } from "@/types/api";
import type { AdminOrderDetailDto, AdminOrderDto, AdminOrderFilters, AdminPlanDto, SavePlanRequest } from "@/types/admin-billing";

const PLANS = "/api/v1/admin/plans";
const ORDERS = "/api/v1/admin/orders";

const ERROR_MESSAGES: Record<string, string> = {
  code_taken: "Mã gói này đã tồn tại.",
  invalid_code: "Mã gói chỉ gồm chữ thường, số và dấu gạch ngang.",
  invalid_duration: "Thời hạn chỉ được 30 hoặc 90 ngày.",
  free_plan_exists: "Đã có gói Miễn phí, không tạo thêm được.",
  kind_immutable: "Không đổi được loại gói sau khi tạo.",
  free_plan_locked: "Gói Miễn phí luôn giá 0đ, luôn mở bán và không xóa được.",
  plan_in_use: "Gói đã có đơn hàng hoặc chu kỳ sử dụng, không xóa được. Hãy tắt mở bán thay vì xóa.",
  already_paid: "Đơn này đã được thanh toán.",
  not_mismatch: "Chỉ từ chối được đơn sai số tiền.",
  note_required: "Cần nhập ghi chú.",
  gateway_unavailable: "Không kết nối được payOS. Thử lại sau ít phút.",
};

/** Câu báo lỗi tiếng Việt: theo code nếu biết, rồi lời server, rồi câu dự phòng. */
export function adminBillingErrorMessage(err: unknown, fallback: string): string {
  const billing = getBillingError(err);
  if (billing) return ERROR_MESSAGES[billing.code] ?? billing.message;
  return err instanceof Error && err.message ? err.message : fallback;
}

export const adminBillingApi = {
  plans: () => apiFetch<AdminPlanDto[]>(PLANS),
  createPlan: (body: SavePlanRequest) => apiFetch<AdminPlanDto>(PLANS, { method: "POST", body: JSON.stringify(body) }),
  updatePlan: (id: string, body: SavePlanRequest) =>
    apiFetch<AdminPlanDto>(`${PLANS}/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  deletePlan: (id: string) => apiFetch<void>(`${PLANS}/${id}`, { method: "DELETE" }),

  orders: (filters: AdminOrderFilters, pageSize = 20) => {
    const params = new URLSearchParams({ page: String(filters.page), pageSize: String(pageSize) });
    if (filters.status) params.set("status", filters.status);
    if (filters.q.trim()) params.set("q", filters.q.trim());
    return apiFetch<PagedResult<AdminOrderDto>>(`${ORDERS}?${params}`);
  },
  order: (orderCode: number) => apiFetch<AdminOrderDetailDto>(`${ORDERS}/${orderCode}`),
  activate: (orderCode: number, note: string) =>
    apiFetch<AdminOrderDto>(`${ORDERS}/${orderCode}/activate`, { method: "POST", body: JSON.stringify({ note }) }),
  reject: (orderCode: number, note: string) =>
    apiFetch<AdminOrderDto>(`${ORDERS}/${orderCode}/reject`, { method: "POST", body: JSON.stringify({ note }) }),
  recheck: (orderCode: number) => apiFetch<AdminOrderDto>(`${ORDERS}/${orderCode}/recheck`, { method: "POST" }),
};

export const PLAN_KIND_LABELS = { free: "Miễn phí", personal: "Cá nhân", family: "Gia đình" } as const;

export const formatVnDateTime = (iso: string | null | undefined) =>
  iso ? new Date(iso).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "—";
