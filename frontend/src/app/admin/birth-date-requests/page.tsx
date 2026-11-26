import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { BirthDateRequestsView } from "@/components/admin/birth-date-requests/BirthDateRequestsView";

export const metadata: Metadata = {
  title: "Yêu cầu sửa ngày sinh – Quản trị USDSS",
};

interface PageProps {
  searchParams: Promise<{ id?: string | string[] }>;
}

/** [UC-150] Administrator duyệt hoặc từ chối yêu cầu sửa ngày sinh; `?id=` mở thẳng chi tiết một yêu cầu. */
export default async function AdminBirthDateRequestsPage({ searchParams }: PageProps) {
  const { id } = await searchParams;
  const initialRequestId = typeof id === "string" && id.trim() ? id.trim() : null;
  return (
    <AdminSection href="/admin/birth-date-requests">
      <BirthDateRequestsView initialRequestId={initialRequestId} />
    </AdminSection>
  );
}
