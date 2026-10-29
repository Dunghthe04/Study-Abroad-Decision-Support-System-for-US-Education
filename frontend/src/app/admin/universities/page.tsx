import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { UniversityListView } from "@/components/admin/universities/UniversityListView";

export const metadata: Metadata = {
  title: "Dữ liệu trường – Quản trị USAS",
};

export default function AdminUniversitiesPage() {
  return (
    <AdminSection href="/admin/universities">
      <UniversityListView />
    </AdminSection>
  );
}
