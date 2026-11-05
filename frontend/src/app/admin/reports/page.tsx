import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";

export const metadata: Metadata = {
  title: "Báo cáo – Quản trị USAS",
};

export default function AdminReportsPage() {
  return <AdminSection href="/admin/reports" />;
}
