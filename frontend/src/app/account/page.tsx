import { AccountView } from "@/components/account/AccountView";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";

export const metadata = {
  title: "Hồ sơ tài khoản – USAS",
  description: "Xem thông tin tài khoản người dùng trên hệ thống USAS",
};

export default function AccountPage() {
  return (
    <ProtectedRoute>
      <main className="py-6">
        <AccountView />
      </main>
    </ProtectedRoute>
  );
}
