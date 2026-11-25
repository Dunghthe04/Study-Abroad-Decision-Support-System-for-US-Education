// Quản trị gói và đơn hàng (docs/billing-impl-spec.md mục 7). Lỗi dạng { code, message } đọc bằng getBillingError.

import { apiFetch } from "@/lib/api";
import { getBillingError } from "@/lib/billing-api";
import type { PagedResult } from "@/types/api";
import type { AdminOrderBuyerInfoDto, AdminOrderDetailDto, AdminOrderDto, AdminOrderFilters, AdminOrderRecheckDto, AdminPlanDto, SavePlanRequest } from "@/types/admin-billing";

const PLANS = "/api/v1/admin/plans";
const ORDERS = "/api/v1/admin/orders";

const ERROR_MESSAGES: Record<string, string> = {
  code_taken: "Mã gói này đã tồn tại.",
  invalid_code: "Mã gói chỉ gồm chữ thường, số và dấu gạch ngang.",
  invalid_duration: "Thời hạn phải từ 1 đến 3650 ngày.",
  invalid_quota_period: "Chu kỳ cấp token không hợp lệ.",
  free_plan_exists: "Đã có gói Miễn phí, không tạo thêm được.",
  kind_immutable: "Không đổi được loại gói sau khi tạo.",
  free_plan_locked: "Gói Miễn phí luôn giá 0đ, luôn mở bán và không xóa được.",
  plan_in_use: "Gói đã có đơn hàng hoặc chu kỳ sử dụng, không xóa được. Hãy tắt mở bán thay vì xóa.",
  already_paid: "Đơn này đã được thanh toán.",
  not_mismatch: "Chỉ từ chối được đơn lệch số tiền.",
  note_required: "Cần nhập ghi chú.",
  gateway_not_paid: "payOS chưa ghi nhận giao dịch đã thanh toán nên không thể kích hoạt.",
  gateway_unavailable: "Không kết nối được payOS. Thử lại sau ít phút.",
  buyer_deleted: "Người mua đã xóa tài khoản nên không thể kích hoạt. Đơn đã chuyển sang Lệch số tiền để hủy và hoàn tiền ngoài hệ thống.",
  buyer_info_not_found: "Không có thông tin lưu trữ người mua đã xóa cho đơn này.",
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
    if (filters.from) params.set("from", filters.from);
    if (filters.to) params.set("to", filters.to);
    return apiFetch<PagedResult<AdminOrderDto>>(`${ORDERS}?${params}`);
  },
  order: (orderCode: number) => apiFetch<AdminOrderDetailDto>(`${ORDERS}/${orderCode}`),
  activate: (orderCode: number, note: string) =>
    apiFetch<AdminOrderDto>(`${ORDERS}/${orderCode}/activate`, { method: "POST", body: JSON.stringify({ note }) }),
  reject: (orderCode: number, note: string) =>
    apiFetch<AdminOrderDto>(`${ORDERS}/${orderCode}/reject`, { method: "POST", body: JSON.stringify({ note }) }),
  recheck: (orderCode: number) => apiFetch<AdminOrderRecheckDto>(`${ORDERS}/${orderCode}/recheck`, { method: "POST" }),
  revealBuyer: (orderCode: number) => apiFetch<AdminOrderBuyerInfoDto>(`${ORDERS}/${orderCode}/reveal-buyer`, { method: "POST" }),
};

export const PLAN_KIND_LABELS = { free: "Miễn phí", personal: "Cá nhân", family: "Gia đình" } as const;

export const formatVnDateTime = (iso: string | null | undefined) =>
  iso ? new Date(iso).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" }) : "—";
