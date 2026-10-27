import type { Metadata } from "next";
import { AgeGate } from "@/components/auth/AgeGate";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { ProfileFlowWizard } from "@/components/flow/ProfileFlowWizard";

export const metadata: Metadata = {
  title: "Phân tích hồ sơ chọn trường – USAS",
  description:
    "Luồng phân tích hồ sơ du học Mỹ 6 bước: mục tiêu, bảng điểm OCR thông minh, chứng chỉ, tài chính, hoạt động ngoại khóa và gợi ý trường tối ưu.",
};

export default function AnalysisPage() {
  return (
    <ProtectedRoute>
      <AgeGate feature="process_student_data">
        <ProfileFlowWizard />
      </AgeGate>
    </ProtectedRoute>
  );
}
