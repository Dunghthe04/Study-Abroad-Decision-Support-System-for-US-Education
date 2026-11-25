import type { Metadata } from "next";
import { connection } from "next/server";
import { AdminSection } from "@/components/admin/AdminSection";
import { CostParametersSettings } from "@/components/admin/parameters/CostParametersSettings";
import { GradeScaleSettings } from "@/components/admin/parameters/GradeScaleSettings";
import { ParameterList } from "@/components/methodology/ParameterList";
import { getMethodologyParameters } from "@/lib/methodology-api";

export const metadata: Metadata = {
  title: "Tham số hệ thống – Quản trị USDSS",
};

/**
 * Tham số hệ thống. Bảng quy đổi điểm [UC-68]: moderator sửa, admin xem.
 * Tham số thuật toán gợi ý [UC-89]: chỉ xem cho cả hai vai trò, không ai sửa trên giao diện (D6, BR-10).
 * Tỷ giá [UC-70] làm sau. Luật tư vấn [UC-88] không làm thành chức năng riêng.
 */
export default async function AdminParametersPage() {
  await connection();
  const parameters = await getMethodologyParameters().catch(() => null);

  return (
    <AdminSection href="/admin/parameters">
      <div className="flex flex-col gap-6">
        <GradeScaleSettings />
        <CostParametersSettings />
        <section className="rounded-xl border border-line bg-surface p-5 sm:p-6">
          <h2 className="font-sans text-lg font-semibold text-foreground">Tham số gợi ý trường và ước tính chi phí</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Chỉ xem, không sửa tại đây. Muốn đổi một tham số thuật toán, gửi yêu cầu kèm lý do cho nhóm phát triển để sửa trong mã nguồn, kiểm thử và tăng phiên bản thuật toán. Phí visa, SEVIS và mức ngân sách là dữ liệu hệ thống, tỉ giá tự cập nhật mỗi ngày.
          </p>
          <div className="mt-5">
            <ParameterList data={parameters} />
          </div>
        </section>
      </div>
    </AdminSection>
  );
}
