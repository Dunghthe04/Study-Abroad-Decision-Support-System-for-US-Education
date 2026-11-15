import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { LeadMessageModerationView } from "@/components/admin/lead-messages/LeadMessageModerationView";

export const metadata: Metadata = {
  title: "Tin nhắn liên hệ – Quản trị USAS",
};

/** [UC-58] Hàng đợi tin nhắn yêu cầu liên hệ bị gắn cờ: Moderator giữ hoặc gỡ, Admin chỉ xem. */
export default function AdminLeadMessagesPage() {
  return (
    <AdminSection href="/admin/lead-messages">
      <LeadMessageModerationView />
    </AdminSection>
  );
}
