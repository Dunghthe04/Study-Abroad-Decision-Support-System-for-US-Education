import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { CenterLeadsListView } from "@/components/center/leads/CenterLeadsListView";

export const metadata: Metadata = {
  title: "Yêu cầu liên hệ nhận được – USAS",
  description: "Quản lý và xử lý các yêu cầu tư vấn, liên hệ từ học sinh và phụ huynh gửi đến trung tâm.",
};

export default function CenterLeadsPage() {
  return (
    <ProtectedRoute allowedRoles={["center", "moderator", "admin"]}>
      <CenterLeadsListView />
    </ProtectedRoute>
  );
}
