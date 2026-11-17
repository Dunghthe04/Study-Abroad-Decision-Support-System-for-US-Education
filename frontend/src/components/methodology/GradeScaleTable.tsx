import type { GradeScaleConfig } from "@/types/academic";

// Số thập phân hiển thị bằng dấu phẩy (8.5 thành 8,5)
function fmt(value: number): string {
  return String(value).replace(".", ",");
}

// Bảng quy đổi điểm thang 10 sang GPA thang 4 đang dùng (UC-89, chỉ xem). Moderator sửa ở trang quản trị (UC-68).
export function GradeScaleTable({ config }: { config: GradeScaleConfig | null }) {
  if (!config || config.rules.length === 0) {
    return <p className="text-sm text-muted-foreground">Chưa tải được bảng quy đổi điểm. Bạn tải lại trang sau ít phút nhé.</p>;
  }

  const rules = [...config.rules].sort((a, b) => b.minScore - a.minScore);

  return (
    <div className="space-y-3">
      <p className="text-sm text-muted-foreground">
        Điểm thang 10 của bạn được quy đổi sang GPA thang 4,0 theo bảng này trước khi so với sinh viên của trường. Nguồn: {config.sourceName}.
      </p>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[360px] text-sm">
          <thead>
            <tr className="border-b border-line text-left text-xs text-muted-foreground">
              <th className="py-2.5 pr-3 font-semibold">Điểm thang 10</th>
              <th className="py-2.5 pr-3 font-semibold">GPA thang 4</th>
              <th className="py-2.5 font-semibold">Xếp loại</th>
            </tr>
          </thead>
          <tbody>
            {rules.map((r) => (
              <tr key={`${r.minScore}-${r.maxScore}`} className="border-b border-line last:border-0">
                <td className="text-numeric py-2.5 pr-3 text-foreground">
                  {fmt(r.minScore)} – {fmt(r.maxScore)}
                </td>
                <td className="text-numeric py-2.5 pr-3 font-medium text-foreground">{fmt(r.gpa4)}</td>
                <td className="py-2.5 text-muted-foreground">{r.letterGrade || "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
