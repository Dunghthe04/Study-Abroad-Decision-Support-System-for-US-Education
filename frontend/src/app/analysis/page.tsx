import type { Metadata } from "next";
import { AgeGate } from "@/components/auth/AgeGate";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { ProfileFlowWizard } from "@/components/flow/ProfileFlowWizard";

export const metadata: Metadata = {
  title: "Phân tích hồ sơ – USDSS",
  description:
    "Luồng phân tích hồ sơ du học Mỹ 6 bước: mục tiêu, bảng điểm OCR thông minh, chứng chỉ, tài chính, hoạt động ngoại khóa và gợi ý trường tối ưu.",
};

export default function AnalysisPage() {
  return (
    // Chỉ học sinh / phụ huynh mới có hồ sơ để lưu nháp; tài khoản khác vào sẽ bị BE trả 403 mỗi lần tự lưu
    <ProtectedRoute allowedRoles={["student", "parent"]}>
      <AgeGate feature="process_student_data">
        <ProfileFlowWizard />
      </AgeGate>
    </ProtectedRoute>
  );
}
