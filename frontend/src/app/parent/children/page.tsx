import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { ParentChildrenHome } from "@/components/parent/ChildDashboard";

// Chức năng 1 "Con của tôi": tự mở con đầu tiên, chưa liên kết thì chỉ sang Tài khoản
export const metadata: Metadata = {
  title: "Con của tôi – USAS",
};

export default function ParentChildrenPage() {
  return (
    <ProtectedRoute allowedRoles={["parent"]}>
      <ParentChildrenHome />
    </ProtectedRoute>
  );
}
