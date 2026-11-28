import type { Metadata } from "next";
import { connection } from "next/server";
import { PolicyPage } from "@/components/policy/PolicyPage";

export const metadata: Metadata = {
  title: "Điều khoản – USDSS",
  description: "Điều khoản sử dụng hệ thống hỗ trợ ra quyết định du học Mỹ USAS (USDSS)",
};

// Nội dung do Administrator soạn và công bố theo phiên bản (UC-103), không viết cứng trong mã
export default async function TermsPage() {
  await connection();
  return <PolicyPage kind="terms_of_use" fallbackTitle="Điều khoản sử dụng" />;
}
