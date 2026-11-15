import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { ReviewGradingView } from "@/components/admin/review-batches/ReviewGradingView";

export const metadata: Metadata = {
  title: "Chấm đợt duyệt mẫu – Quản trị USAS",
};

interface Props {
  params: Promise<{ batchId: string }>;
}

export default async function AdminReviewBatchPage({ params }: Props) {
  const { batchId } = await params;
  return (
    <AdminSection href="/admin/review-batches">
      <ReviewGradingView batchId={batchId} />
    </AdminSection>
  );
}
