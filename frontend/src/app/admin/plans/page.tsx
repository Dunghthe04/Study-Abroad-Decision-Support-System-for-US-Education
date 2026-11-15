import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";

export const metadata: Metadata = {
  title: "Gói và giao dịch – Quản trị USAS",
};

export default function AdminPlansPage() {
  return <AdminSection href="/admin/plans" />;
}
