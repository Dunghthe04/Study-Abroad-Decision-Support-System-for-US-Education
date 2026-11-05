import type { Metadata } from "next";
import { RedirectToAnalysis } from "@/components/profile/RedirectToAnalysis";

export const metadata: Metadata = {
  title: "Chuyển hướng Phân tích điểm học thuật – USAS",
  description: "Trang phân tích GPA đã được nâng cấp sang Luồng phân tích một cửa tại /analysis.",
};

export default function AcademicAnalysisRoutePage() {
  return <RedirectToAnalysis sourceTitle="Phân tích điểm học thuật" />;
}
