import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { OrdersHistoryView } from "@/components/billing/OrdersHistoryView";

export const metadata: Metadata = {
  title: "Giao dịch – USDSS",
};

export default function OrdersPage() {
  return (
    <div className="py-6">
      <ProtectedRoute allowedRoles={["student", "parent"]}>
        <OrdersHistoryView />
      </ProtectedRoute>
    </div>
  );
}
