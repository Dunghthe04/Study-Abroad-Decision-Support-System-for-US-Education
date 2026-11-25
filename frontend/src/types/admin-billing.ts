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
  adminNote: string | null;
  handledBy: string | null;
  gatewayTransactionId: string | null;
  receivedAmountVnd: number | null;
  deletedBuyerCode: string | null;
}

export interface AdminOrderDetailDto extends AdminOrderDto {
  payloadJson: string | null;
}

export interface AdminOrderRecheckDto {
  orderCode: number;
  orderStatus: OrderStatus;
  gatewayStatus: string;
  gatewayAmountPaid: number;
  canActivate: boolean;
  checkedAt: string;
}

export interface AdminOrderBuyerInfoDto {
  buyerCode: string;
  fullName: string;
  email: string;
  retainUntilUtc: string;
}

export interface AdminOrderFilters {
  status: OrderStatus | "";
  q: string;
  from: string;
  to: string;
  page: number;
}
