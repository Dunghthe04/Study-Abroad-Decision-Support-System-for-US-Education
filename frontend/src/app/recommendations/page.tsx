import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { RecommendationPanel } from "@/components/recommendations/RecommendationPanel";

export const metadata = {
  title: "Gợi ý trường – USAS",
  description: "Lọc trường đại học Mỹ phù hợp theo mô hình phân tầng 4 nhóm trường, thuật toán AHP & SAW.",
};

export default function RecommendationsPage() {
  return (
    <ProtectedRoute>
      <RecommendationPanel />
    </ProtectedRoute>
  );
}
