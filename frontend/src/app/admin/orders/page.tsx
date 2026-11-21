import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { OrdersTab } from "@/components/admin/billing/OrdersTab";

export const metadata: Metadata = {
  title: "Đơn hàng – Quản trị USDSS",
};

/** [UC-132..134] Đơn hàng: lọc, xem chi tiết, đối soát, kích hoạt, từ chối. */
export default function AdminOrdersPage() {
  return (
    <AdminSection href="/admin/orders">
      <div className="flex min-h-0 flex-1 flex-col">
        <OrdersTab />
      </div>
    </AdminSection>
  );
}
