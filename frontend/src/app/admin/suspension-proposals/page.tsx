import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { SuspensionProposalsView } from "@/components/admin/suspension/SuspensionProposalsView";

export const metadata: Metadata = {
  title: "Đề nghị tạm ngừng trung tâm – Quản trị USAS",
};

/** [UC-58] Đề nghị tạm ngừng trung tâm bị gỡ tin lặp lại: Moderator gửi, Admin quyết định. */
export default function AdminSuspensionProposalsPage() {
  return (
    <AdminSection href="/admin/suspension-proposals">
      <SuspensionProposalsView />
    </AdminSection>
  );
}
