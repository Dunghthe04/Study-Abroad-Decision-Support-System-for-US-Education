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
  estimatedQuestions: number;
  isActive: boolean;
  displayOrder: number;
}

export interface AdminOrderDto extends OrderDto {
  buyerName: string | null;
  buyerEmail: string | null;
  adminNote: string | null;
  handledBy: string | null;
}

export interface AdminOrderDetailDto extends AdminOrderDto {
  payloadJson: string | null;
}

export interface AdminOrderFilters {
  status: OrderStatus | "";
  q: string;
  page: number;
}
