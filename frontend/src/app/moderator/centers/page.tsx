import type { Metadata } from "next";
import { AdminCenterListView } from "@/components/admin/centers/AdminCenterListView";

export const metadata: Metadata = {
  title: "Danh bạ trung tâm tư vấn – Moderator USAS",
  description: "Quản lý danh bạ trung tâm tư vấn, thêm, xác minh, sửa hoặc ẩn trung tâm và cấp tài khoản trung tâm",
};

export default function ModeratorCentersPage() {
  return (
    <div className="space-y-4">
      <AdminCenterListView />
    </div>
  );
}
