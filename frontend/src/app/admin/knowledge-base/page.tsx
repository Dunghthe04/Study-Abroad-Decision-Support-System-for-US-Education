import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { KnowledgeBaseListView } from "@/components/admin/knowledge-base/KnowledgeBaseListView";

export const metadata: Metadata = {
  title: "Kho tri thức – Quản trị USAS",
};

export default function AdminKnowledgeBasePage() {
  return (
    <AdminSection href="/admin/knowledge-base">
      <KnowledgeBaseListView />
    </AdminSection>
  );
}