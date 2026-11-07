import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { AdminCenterListView } from "@/components/admin/centers/AdminCenterListView";

export const metadata: Metadata = {
  title: "Trung tâm tư vấn – Quản trị USAS",
};

export default function AdminCentersPage() {
  return (
    <AdminSection href="/admin/centers">
      <AdminCenterListView />
    </AdminSection>
  );
}
