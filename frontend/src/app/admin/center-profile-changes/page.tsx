import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { CenterProfileChangesView } from "@/components/admin/centers/CenterProfileChangesView";

export const metadata: Metadata = {
  title: "Kiểm tra thay đổi trung tâm – Quản trị USDSS",
};

/** [UC-114] Moderator kiểm tra thay đổi website / fanpage do trung tâm tự sửa. */
export default function AdminCenterProfileChangesPage() {
  return (
    <AdminSection href="/admin/center-profile-changes">
      <CenterProfileChangesView />
    </AdminSection>
  );
}
