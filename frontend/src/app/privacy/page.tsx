import type { Metadata } from "next";
import { connection } from "next/server";
import { PolicyPage } from "@/components/policy/PolicyPage";

export const metadata: Metadata = {
  title: "Chính sách bảo mật – USDSS",
  description: "Chính sách quyền riêng tư và bảo vệ dữ liệu cá nhân của hệ thống USAS (USDSS)",
};

// Nội dung do Administrator soạn và công bố theo phiên bản (UC-103), không viết cứng trong mã
export default async function PrivacyPage() {
  await connection();
  return <PolicyPage kind="privacy_policy" fallbackTitle="Chính sách bảo mật & Quyền riêng tư" />;
}
