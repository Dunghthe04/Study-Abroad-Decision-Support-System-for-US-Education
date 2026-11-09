import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";

export const metadata: Metadata = {
  title: "Tri thức – Quản trị USAS",
};

export default function AdminKnowledgePage() {
  return <AdminSection href="/admin/knowledge" />;
}
