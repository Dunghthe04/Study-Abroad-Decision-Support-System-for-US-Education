import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { CenterReviewsPortalView } from "@/components/center/CenterReviewsPortalView";

export const metadata: Metadata = {
  title: "Đánh giá trung tâm – USDSS",
  description: "Xem, phản hồi công khai hoặc báo cáo đánh giá từ học sinh và phụ huynh về trung tâm.",
};

export default function CenterReviewsPage() {
  return (
    <ProtectedRoute allowedRoles={["center"]}>
      <CenterReviewsPortalView />
    </ProtectedRoute>
  );
}
