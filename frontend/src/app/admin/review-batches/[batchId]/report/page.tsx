import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { ReviewReportView } from "@/components/admin/review-batches/ReviewReportView";

export const metadata: Metadata = {
  title: "Báo cáo duyệt mẫu – Quản trị USAS",
};

interface Props {
  params: Promise<{ batchId: string }>;
}

export default async function AdminReviewBatchReportPage({ params }: Props) {
  const { batchId } = await params;
  return (
    <AdminSection href="/admin/review-batches">
      <ReviewReportView batchId={batchId} />
    </AdminSection>
  );
}
