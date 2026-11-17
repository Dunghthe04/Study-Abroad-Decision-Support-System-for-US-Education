import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { ScholarshipListView } from "@/components/admin/scholarships/ScholarshipListView";

export const metadata: Metadata = {
  title: "Học bổng – Quản trị USAS",
};

export default function AdminScholarshipsPage() {
  return (
    <AdminSection href="/admin/scholarships">
      <ScholarshipListView />
    </AdminSection>
  );
}
