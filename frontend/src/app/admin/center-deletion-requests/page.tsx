import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { CenterDeletionRequestsView } from "@/components/admin/center-deletion/CenterDeletionRequestsView";

export const metadata: Metadata = {
  title: "Yêu cầu xóa trung tâm – Quản trị USAS",
};

/** [UC-93] Administrator xử lý yêu cầu xóa tài khoản và dữ liệu trung tâm. */
export default function AdminCenterDeletionRequestsPage() {
  return (
    <AdminSection href="/admin/center-deletion-requests">
      <CenterDeletionRequestsView />
    </AdminSection>
  );
}
