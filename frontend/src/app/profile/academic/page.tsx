import type { Metadata } from "next";
import { RedirectToAnalysis } from "@/components/profile/RedirectToAnalysis";

export const metadata: Metadata = {
  title: "Chuyển hướng Hồ sơ học thuật – USAS",
  description: "Trang nhập điểm học thuật đã được nâng cấp sang Luồng phân tích một cửa tại /analysis.",
};

export default function AcademicProfilePage() {
  return <RedirectToAnalysis sourceTitle="Hồ sơ học thuật & Bảng điểm" />;
}
