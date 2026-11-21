import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { AiQualityView } from "@/components/admin/ai-quality/AiQualityView";

export const metadata: Metadata = {
  title: "Chất lượng AI – Quản trị USDSS",
};

export default function AdminAiQualityPage() {
  return (
    <AdminSection href="/admin/ai-quality">
      <AiQualityView />
    </AdminSection>
  );
}
