import type { Metadata } from "next";
import { AgeGate } from "@/components/auth/AgeGate";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { RecommendationView } from "@/components/recommendation/RecommendationView";

export const metadata: Metadata = {
  title: "Trường gợi ý – USAS",
  description: "Danh sách trường Khó vào, Vừa sức, An toàn hơn và khối cần học bổng lớn theo hồ sơ của bạn.",
};

// ?run=1: vừa gửi hồ sơ xong, chạy gợi ý mới. Không có thì đọc lần đã lưu.
export default async function RecommendationsPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { run } = await searchParams;

  return (
    <ProtectedRoute>
      <AgeGate feature="process_student_data">
        <RecommendationView autoRun={run === "1"} />
      </AgeGate>
    </ProtectedRoute>
  );
}
