import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { SensitivityView } from "@/components/admin/sensitivity/SensitivityView";

export const metadata: Metadata = {
  title: "Độ nhạy tham số – Quản trị USDSS",
};

/** [UC-85] Phân tích độ nhạy tham số thuật toán gợi ý trường. Chỉ admin. */
export default function AdminSensitivityPage() {
  return (
    <AdminSection href="/admin/sensitivity">
      <SensitivityView />
    </AdminSection>
  );
}
