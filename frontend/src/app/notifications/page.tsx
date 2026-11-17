import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { NotificationsView } from "@/components/notifications/NotificationsView";

export const metadata: Metadata = {
  title: "Thông báo – USAS",
  description: "Thông báo của bạn trong hệ thống",
};

/** [UC-110] Trang thông báo của người đang đăng nhập. */
export default function NotificationsPage() {
  return (
    <div className="py-6">
      <ProtectedRoute>
        <NotificationsView />
      </ProtectedRoute>
    </div>
  );
}
