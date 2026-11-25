// Quản trị gói và đơn hàng (docs/billing-impl-spec.md mục 7). Admin được xem số token thô.

import type { OrderDto, OrderStatus, PlanDto, PlanKind } from "@/types/billing";

export interface AdminPlanDto extends PlanDto {
  id: string;
  tokenQuota: number;
  isActive: boolean;
  displayOrder: number;
  activeCycles: number;
  orderCount: number;
}

/** POST gửi cả code và kind; PUT bỏ code và kind (đổi kind bị chặn bằng kind_immutable). */
export interface SavePlanRequest {
  code?: string;
  kind?: PlanKind;
  name: string;
  description: string | null;
  priceVnd: number;
  durationDays: number | null;
  tokenQuota: number;
  quotaPeriodDays: number | null;
  isActive: boolean;
  displayOrder: number;
}

export interface AdminOrderDto extends OrderDto {
  buyerName: string | null;
  buyerEmail: string | null;
  /** [UC-75 lớp 2] Mã người mua ngẫu nhiên khi người mua đã xóa tài khoản; họ tên, email xem riêng qua retainedBuyer. */
  buyerCode: string | null;
  adminNote: string | null;
  handledBy: string | null;
}

export interface AdminOrderDetailDto extends AdminOrderDto {
  payloadJson: string | null;
}

/** [UC-75 lớp 2] Họ tên, email người mua đã xóa tài khoản, giữ để đối soát và hoàn tiền. Mỗi lần xem được ghi nhật ký. */
export interface RetainedBuyerDto {
  buyerCode: string;
  fullName: string;
  email: string;
  retainUntil: string;
}

export interface AdminOrderFilters {
  status: OrderStatus | "";
  q: string;
  page: number;
}
