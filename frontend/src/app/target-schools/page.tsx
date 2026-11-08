import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { TargetSchoolListView } from "@/components/target-schools/TargetSchoolListView";

export const metadata: Metadata = {
  title: "Danh sách trường mục tiêu – USAS",
  description: "Theo dõi danh sách các trường đại học mục tiêu, phân nhóm Khó vào - Vừa sức - An toàn và dòng thời gian hạn nộp hồ sơ.",
};

export default function TargetSchoolsPage() {
  return (
    <ProtectedRoute allowedRoles={["student", "parent"]}>
      <TargetSchoolListView />
    </ProtectedRoute>
  );
}
