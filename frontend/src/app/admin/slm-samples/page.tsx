import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { SlmSampleListView } from "@/components/admin/slm-samples/SlmSampleListView";

export const metadata: Metadata = {
  title: "Mẫu huấn luyện SLM – Quản trị USDSS",
};

export default function AdminSlmSamplesPage() {
  return (
    <AdminSection href="/admin/slm-samples">
      <SlmSampleListView />
    </AdminSection>
  );
}
