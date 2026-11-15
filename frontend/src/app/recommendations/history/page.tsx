import type { Metadata } from "next";
import { AgeGate } from "@/components/auth/AgeGate";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { RecommendationHistory } from "@/components/recommendation/RecommendationHistory";

// [UC-87] Trang lịch sử gợi ý: các lần chạy trước và so sánh hai lần
export const metadata: Metadata = {
  title: "Lịch sử gợi ý – USAS",
  description: "Xem lại các lần gợi ý trường trước đây và so sánh hai lần để thấy trường nào đổi nhóm, đổi thứ hạng.",
};

export default function RecommendationHistoryPage() {
  return (
    <ProtectedRoute>
      <AgeGate feature="process_student_data">
        <RecommendationHistory />
      </AgeGate>
    </ProtectedRoute>
  );
}
