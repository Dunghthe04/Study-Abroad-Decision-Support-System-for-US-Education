import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";

export const metadata: Metadata = {
  title: "Dữ liệu trường – Quản trị USAS",
};

export default function AdminUniversitiesPage() {
  return <AdminSection href="/admin/universities" />;
}
