import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";

export const metadata: Metadata = {
  title: "Tham số hệ thống – Quản trị USAS",
};

export default function AdminParametersPage() {
  return <AdminSection href="/admin/parameters" />;
}
