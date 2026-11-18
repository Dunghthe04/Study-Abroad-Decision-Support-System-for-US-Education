import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { NotificationsView } from "@/components/notifications/NotificationsView";

export const metadata: Metadata = {
  title: "Thông báo – USAS",
  description: "Thông báo của bạn trong hệ thống",
};

// [UC-110] Mọi vai trò đã đăng nhập xem thông báo của chính mình
export default function NotificationsPage() {
  return (
    <div className="py-6">
      <ProtectedRoute>
        <NotificationsView />
      </ProtectedRoute>
    </div>
  );
}
