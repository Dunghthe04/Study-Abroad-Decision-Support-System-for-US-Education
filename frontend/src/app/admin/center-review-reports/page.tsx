import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { CenterReviewReportsView } from "@/components/admin/centers/CenterReviewReportsView";

export const metadata: Metadata = {
  title: "Báo cáo đánh giá trung tâm – Quản trị USDSS",
};

/** [UC-151 / UC-53] Moderator xem và xử lý báo cáo đánh giá trung tâm vi phạm. */
export default function AdminCenterReviewReportsPage() {
  return (
    <AdminSection href="/admin/center-review-reports">
      <CenterReviewReportsView />
    </AdminSection>
  );
}
