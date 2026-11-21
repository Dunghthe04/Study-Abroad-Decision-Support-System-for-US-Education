import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { AdminUsersView } from "@/components/admin/AdminUsersView";

export const metadata: Metadata = {
  title: "Quản trị người dùng – Quản trị & Kiểm duyệt USDSS",
  description: "Quản trị viên quản trị danh sách tài khoản, khóa/mở khóa và cấp tài khoản kiểm duyệt viên (UC-60, UC-61, UC-62, UC-63)",
};

/** [UC-60..63] Khu vực quản trị & kiểm duyệt — quản trị tài khoản người dùng. */
export default function AdminUsersPage() {
  return (
    <AdminSection href="/admin/users">
      <AdminUsersView />
    </AdminSection>
  );
}
