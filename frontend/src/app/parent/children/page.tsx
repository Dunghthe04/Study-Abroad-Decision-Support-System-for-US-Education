import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { ParentChildrenHome } from "@/components/parent/ChildDashboard";
import { toChildTab } from "@/lib/child-tabs";

// Chức năng 1 "Con của tôi": tự mở con đầu tiên, chưa liên kết thì chỉ sang Tài khoản
export const metadata: Metadata = {
  title: "Con của tôi – USAS",
};

interface Props {
  searchParams: Promise<{ tab?: string | string[] }>;
}

export default async function ParentChildrenPage({ searchParams }: Props) {
  const { tab } = await searchParams;
  return (
    <ProtectedRoute allowedRoles={["parent"]}>
      <ParentChildrenHome tab={toChildTab(tab)} />
    </ProtectedRoute>
  );
}
