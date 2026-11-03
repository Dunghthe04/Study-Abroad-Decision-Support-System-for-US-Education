import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { AdminUsersView } from "@/components/admin/AdminUsersView";

export const metadata: Metadata = {
  title: "Quản trị người dùng – USAS",
  description: "Administrator quản lý tài khoản và tạo moderator (UC-63)",
};

/** [UC-63] Khu vực admin — chỉ role admin. */
export default function AdminUsersPage() {
  return (
    <ProtectedRoute allowedRoles={["admin"]}>
      <AdminUsersView />
    </ProtectedRoute>
  );
}
