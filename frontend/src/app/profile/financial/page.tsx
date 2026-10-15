import type { Metadata } from "next";
import { RedirectToAnalysis } from "@/components/profile/RedirectToAnalysis";

export const metadata: Metadata = {
  title: "Chuyển hướng Hồ sơ tài chính & Ngoại khóa – USAS",
  description: "Trang khai báo tài chính và ngoại khóa đã được tích hợp vào Luồng phân tích một cửa tại /analysis.",
};

export default function FinancialProfilePage() {
  return <RedirectToAnalysis sourceTitle="Hồ sơ tài chính & Hoạt động ngoại khóa" />;
}
