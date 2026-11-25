import { Badge } from "@/components/ui/badge";
import type { OrderStatus } from "@/types/billing";

export const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
  pending: "Chờ thanh toán",
  paid: "Đã thanh toán",
  mismatch: "Lệch số tiền",
  expired: "Hết hạn",
  cancelled: "Đã hủy",
};

const VARIANTS = { pending: "warn", paid: "ok", mismatch: "risk", expired: "neutral", cancelled: "neutral" } as const;

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  const key = status.toLowerCase() as OrderStatus;
  return <Badge variant={VARIANTS[key] ?? "neutral"}>{ORDER_STATUS_LABELS[key] ?? status}</Badge>;
}
