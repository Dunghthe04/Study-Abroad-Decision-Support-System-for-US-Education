import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { AiUsageView } from "@/components/admin/ai-usage/AiUsageView";

export const metadata: Metadata = {
  title: "Quản lý token – Quản trị USAS",
};

export default function AdminAiUsagePage() {
  return (
    <AdminSection href="/admin/ai-usage">
      <AiUsageView />
    </AdminSection>
  );
}
