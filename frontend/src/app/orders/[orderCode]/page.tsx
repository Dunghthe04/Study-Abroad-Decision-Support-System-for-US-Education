import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { OrderView } from "@/components/billing/OrderView";

export const metadata: Metadata = {
  title: "Đơn hàng – USDSS",
};

interface Props {
  params: Promise<{ orderCode: string }>;
}

export default async function OrderPage({ params }: Props) {
  const { orderCode } = await params;
  return (
    <div className="py-8 sm:py-10">
      <ProtectedRoute allowedRoles={["student", "parent"]}>
        <OrderView orderCode={decodeURIComponent(orderCode)} />
      </ProtectedRoute>
    </div>
  );
}
