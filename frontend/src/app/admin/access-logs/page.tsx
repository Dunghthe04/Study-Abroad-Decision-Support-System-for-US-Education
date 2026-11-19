import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { AccessLogsView } from "@/components/admin/access-logs/AccessLogsView";

export const metadata: Metadata = {
  title: "Nhật ký truy cập – Quản trị USDSS",
};

/**
 * [UC-97] Administrator xem và lọc nhật ký truy cập và xuất dữ liệu nhạy cảm
 * (tài liệu xác minh trung tâm, tin nhắn bị báo cáo, xuất mẫu ở UC-102).
 * Quy tắc: Nhật ký bất biến, không sửa và không xóa được.
 */
export default function AdminAccessLogsPage() {
  return (
    <AdminSection href="/admin/access-logs">
      <AccessLogsView />
    </AdminSection>
  );
}

