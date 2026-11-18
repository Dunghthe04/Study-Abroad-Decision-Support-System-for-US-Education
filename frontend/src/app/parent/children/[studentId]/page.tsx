import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { ChildDashboard } from "@/components/parent/ChildDashboard";

// Chức năng 1 "Con của tôi": hồ sơ, gợi ý, trường mục tiêu, lịch sử và kịch bản của một con. Chỉ xem.
export const metadata: Metadata = {
  title: "Con của tôi – USAS",
};

interface Props {
  params: Promise<{ studentId: string }>;
}

export default async function ParentChildPage({ params }: Props) {
  const { studentId } = await params;
  return (
    <ProtectedRoute allowedRoles={["parent"]}>
      <ChildDashboard studentId={decodeURIComponent(studentId)} />
    </ProtectedRoute>
  );
}
