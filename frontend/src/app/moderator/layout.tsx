import type { Metadata } from "next";
import { ModeratorShell } from "@/components/moderator/ModeratorShell";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { MODERATOR_ROLES } from "@/lib/moderator-nav";

export const metadata: Metadata = {
  title: "Kiểm duyệt & Vận hành – USAS",
  description: "Khu vực kiểm duyệt trung tâm tư vấn, tri thức và diễn đàn dành cho Moderator",
};

export default function ModeratorLayout({ children }: { children: React.ReactNode }) {
  return (
    <ProtectedRoute
      allowedRoles={MODERATOR_ROLES}
      deniedMessage={<>Khu vực này chỉ dành cho tài khoản <strong>Moderator</strong> hoặc <strong>Quản trị viên</strong>.</>}
    >
      <ModeratorShell>{children}</ModeratorShell>
    </ProtectedRoute>
  );
}
