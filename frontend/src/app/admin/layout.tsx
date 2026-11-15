import type { Metadata } from "next";
import { AdminShell } from "@/components/admin/AdminShell";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { ADMIN_ROLES } from "@/lib/admin-nav";

export const metadata: Metadata = {
  title: "Quản trị – USAS",
};

export default function AdminLayout({ children }: LayoutProps<"/admin">) {
  return (
    <ProtectedRoute
      allowedRoles={ADMIN_ROLES}
      deniedMessage={<>Khu vực quản trị chỉ dành cho tài khoản <strong>Quản trị viên</strong> hoặc <strong>Moderator</strong>.</>}
    >
      <AdminShell>{children}</AdminShell>
    </ProtectedRoute>
  );
}
