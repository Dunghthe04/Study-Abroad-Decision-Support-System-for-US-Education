import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { ChildDashboard } from "@/components/parent/ChildDashboard";
import { toChildTab } from "@/lib/child-tabs";

// Chức năng 1 "Con của tôi": hồ sơ, gợi ý, trường mục tiêu, lịch sử và kịch bản của một con. Chỉ xem.
export const metadata: Metadata = {
  title: "Con của tôi – USDSS",
};

interface Props {
  params: Promise<{ studentId: string }>;
  searchParams: Promise<{ tab?: string | string[] }>;
}

export default async function ParentChildPage({ params, searchParams }: Props) {
  const { studentId } = await params;
  const { tab } = await searchParams;
  return (
    <ProtectedRoute allowedRoles={["parent"]}>
      <ChildDashboard studentId={decodeURIComponent(studentId)} initialTab={toChildTab(tab)} />
    </ProtectedRoute>
  );
}
