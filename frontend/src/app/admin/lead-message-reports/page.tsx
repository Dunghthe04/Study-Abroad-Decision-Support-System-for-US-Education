import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { LeadMessageReportsView } from "@/components/admin/lead-messages/LeadMessageReportsView";

export const metadata: Metadata = {
  title: "Tin nhắn bị báo cáo – Quản trị USAS",
};

/** [UC-58] Admin xem tin nhắn liên hệ bị báo cáo, bỏ qua hoặc khóa tài khoản người gửi. */
export default function AdminLeadMessageReportsPage() {
  return (
    <AdminSection href="/admin/lead-message-reports">
      <LeadMessageReportsView />
    </AdminSection>
  );
}
