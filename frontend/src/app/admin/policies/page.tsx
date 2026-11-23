import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { PoliciesView } from "@/components/admin/policies/PoliciesView";

export const metadata: Metadata = {
  title: "Chính sách và điều khoản – Quản trị USDSS",
};

/** [UC-103] Quản lý chính sách và điều khoản theo phiên bản. Chỉ admin. */
export default function AdminPoliciesPage() {
  return (
    <AdminSection href="/admin/policies">
      <PoliciesView />
    </AdminSection>
  );
}
