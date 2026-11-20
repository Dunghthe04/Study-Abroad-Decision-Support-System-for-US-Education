import type { Metadata } from "next";
import { connection } from "next/server";
import { MethodologyView } from "@/components/methodology/MethodologyView";
import { getGradeScaleConfig } from "@/lib/academic-api";
import { getMethodologyParameters } from "@/lib/methodology-api";
import type { GradeScaleConfig } from "@/types/academic";
import type { MethodologyParameters } from "@/types/methodology";

export const metadata: Metadata = {
  title: "Cách hệ thống xếp trường – USDSS",
  description:
    "Hệ thống so hồ sơ của bạn với sinh viên từng trường đã nhận rồi xếp vào nhóm Khó vào, Vừa sức, An toàn hơn. Có hình minh họa và ví dụ có số.",
};

// Nội dung trang này mô tả đúng công thức trong backend (SchoolGrouper, SchoolRanker, HardFilter).
// Các số trong ví dụ được kiểm bằng test backend (MethodologyExampleTests): đổi công thức thì test báo và trang này phải sửa theo.

export default async function MethodologyPage() {
  await connection();

  // Bảng tham số và bảng quy đổi điểm lấy từ backend (UC-89). Lỗi thì chỉ phần bảng đó báo chưa tải được,
  // các phần giải thích khác vẫn hiện.
  const [parameters, gradeScale]: [MethodologyParameters | null, GradeScaleConfig | null] = await Promise.all([
    getMethodologyParameters().catch(() => null),
    getGradeScaleConfig().catch(() => null),
  ]);

  return <MethodologyView parameters={parameters} gradeScale={gradeScale} />;
}
