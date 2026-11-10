import type { Metadata } from "next";
import { AdminSection } from "@/components/admin/AdminSection";
import { GradeScaleSettings } from "@/components/admin/parameters/GradeScaleSettings";

export const metadata: Metadata = {
  title: "Tham số hệ thống – Quản trị USAS",
};

/** [UC-68] Tham số hệ thống: hiện có bảng quy đổi điểm (moderator sửa, admin xem). UC-69, UC-70, UC-88 làm sau. */
export default function AdminParametersPage() {
  return (
    <AdminSection href="/admin/parameters">
      <div className="flex flex-col gap-6">
        <h1 className="text-h2">Tham số hệ thống</h1>
        <GradeScaleSettings />
      </div>
    </AdminSection>
  );
}
