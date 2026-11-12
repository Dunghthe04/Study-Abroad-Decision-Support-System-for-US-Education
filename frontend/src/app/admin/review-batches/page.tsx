import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { ReviewBatchesView } from "@/components/admin/review-batches/ReviewBatchesView";

export const metadata: Metadata = {
  title: "Duyệt mẫu – Quản trị USAS",
};

export default function AdminReviewBatchesPage() {
  return (
    <AdminSection href="/admin/review-batches">
      <ReviewBatchesView />
    </AdminSection>
  );
}
