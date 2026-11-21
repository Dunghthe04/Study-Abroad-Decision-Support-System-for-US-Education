import type { Metadata } from "next";
import { AdminShell } from "@/components/admin/AdminShell";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { ADMIN_ROLES } from "@/lib/admin-nav";

export const metadata: Metadata = {
  title: "Quản trị – USDSS",
};

export default function AdminLayout({ children }: LayoutProps<"/admin">) {
  return (
    <ProtectedRoute allowedRoles={ADMIN_ROLES}>
      <AdminShell>{children}</AdminShell>
    </ProtectedRoute>
  );
}
