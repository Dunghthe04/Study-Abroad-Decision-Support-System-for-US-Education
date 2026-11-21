import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { PlansTab } from "@/components/admin/billing/PlansTab";

export const metadata: Metadata = {
  title: "Gói dịch vụ – Quản trị USDSS",
};

/** [UC-79] Quản lý gói dịch vụ. */
export default function AdminPlansPage() {
  return (
    <AdminSection href="/admin/plans">
      <div className="flex min-h-0 flex-1 flex-col">
        <PlansTab />
      </div>
    </AdminSection>
  );
}
