import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { AdminPlansView } from "@/components/admin/billing/AdminPlansView";

export const metadata: Metadata = {
  title: "Gói và giao dịch – Quản trị USDSS",
};

/** [UC-79, UC-132..134] Quản lý gói dịch vụ và đơn hàng. */
export default function AdminPlansPage() {
  return (
    <AdminSection href="/admin/plans">
      <AdminPlansView />
    </AdminSection>
  );
}
