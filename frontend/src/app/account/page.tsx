import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { AccountView } from "@/components/account/AccountView";

export const metadata: Metadata = {
  title: "Hồ sơ của tôi – USAS",
  description: "Quản lý thông tin tài khoản và phiên đăng nhập cá nhân",
};

export default function AccountPage() {
  return (
    <div className="py-6">
      <ProtectedRoute>
        <AccountView />
      </ProtectedRoute>
    </div>
  );
}
