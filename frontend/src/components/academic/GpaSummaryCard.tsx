import type { AcademicAnalysisResponse } from "@/types/academic";

interface GpaSummaryCardProps {
  analysis: AcademicAnalysisResponse;
}

/**
 * [USAS-365] Thẻ hiển thị tổng quan kết quả phân tích GPA:
 * - Điểm Unweighted GPA & Weighted GPA thang 4.0
 * - Điểm trung bình hệ 10
 * - Đánh giá xu hướng học tập (Growth Mindset)
 * - Khuyến cáo tham khảo chuẩn WES
 */
export function GpaSummaryCard({ analysis }: GpaSummaryCardProps) {
  const getTrendBadge = (trend: string) => {
    switch (trend) {
      case "upward":
        return {
          label: "Xu hướng tiến bộ (Upward Trend)",
          bg: "bg-emerald-50 text-emerald-800 border-emerald-200",
          icon: "📈",
        };
      case "downward":
        return {
          label: "Xu hướng cần lưu ý (Downward Trend)",
          bg: "bg-amber-50 text-amber-800 border-amber-200",
          icon: "📉",
        };
      default:
        return {
          label: "Phong độ ổn định (Consistent)",
          bg: "bg-blue-50 text-blue-800 border-blue-200",
          icon: "⚖️",
        };
    }
  };

  const trendBadge = getTrendBadge(analysis.trend);

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-100 pb-5">
        <div>
          <h2 className="text-xl font-bold text-slate-900">
            Năng Lực Học Thuật & GPA Thang 4.0
          </h2>
          <p className="text-xs text-slate-500 mt-1">
            Quy đổi theo {analysis.scaleSource}
          </p>
        </div>
        <div
          className={`inline-flex items-center gap-2 rounded-full border px-3 py-1.5 text-xs font-semibold ${trendBadge.bg}`}
        >
          <span>{trendBadge.icon}</span>
          <span>{trendBadge.label}</span>
        </div>
      </div>

      {/* Grid thống kê GPA */}
      <div className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
        <div className="rounded-lg bg-slate-50 p-4 border border-slate-100">
          <span className="text-xs font-medium text-slate-500">Unweighted GPA</span>
          <div className="mt-2 flex items-baseline gap-1">
            <span className="text-3xl font-extrabold text-blue-600">
              {analysis.unweightedGpa.toFixed(2)}
            </span>
            <span className="text-xs text-slate-400">/ 4.0</span>
          </div>
          <span className="mt-1 block text-[11px] text-slate-500">
            Thang chuẩn không trọng số
          </span>
        </div>

        <div className="rounded-lg bg-slate-50 p-4 border border-slate-100">
          <span className="text-xs font-medium text-slate-500">Weighted GPA</span>
          <div className="mt-2 flex items-baseline gap-1">
            <span className="text-3xl font-extrabold text-emerald-600">
              {analysis.weightedGpa.toFixed(2)}
            </span>
            <span className="text-xs text-slate-400">/ 4.0</span>
          </div>
          <span className="mt-1 block text-[11px] text-slate-500">
            Có tính tín chỉ / môn chuyên
          </span>
        </div>

        <div className="rounded-lg bg-slate-50 p-4 border border-slate-100">
          <span className="text-xs font-medium text-slate-500">Điểm TB Hệ 10</span>
          <div className="mt-2 flex items-baseline gap-1">
            <span className="text-3xl font-extrabold text-slate-800">
              {analysis.rawAverage.toFixed(2)}
            </span>
            <span className="text-xs text-slate-400">/ 10.0</span>
          </div>
          <span className="mt-1 block text-[11px] text-slate-500">
            Trung bình toàn bộ môn học
          </span>
        </div>

        <div className="rounded-lg bg-slate-50 p-4 border border-slate-100">
          <span className="text-xs font-medium text-slate-500">Quy Mô Dữ Liệu</span>
          <div className="mt-2 flex items-baseline gap-1">
            <span className="text-3xl font-extrabold text-slate-800">
              {analysis.totalSubjects}
            </span>
            <span className="text-xs text-slate-400">môn</span>
          </div>
          <span className="mt-1 block text-[11px] text-slate-500">
            Đánh giá qua {analysis.totalTerms} học kỳ
          </span>
        </div>
      </div>

      {/* Đánh giá nhận xét xu hướng */}
      <div className="mt-5 rounded-lg bg-slate-50 p-4 border border-slate-100">
        <h4 className="text-xs font-bold uppercase tracking-wider text-slate-700">
          Nhận xét xu hướng học tập
        </h4>
        <p className="mt-1 text-sm text-slate-700 leading-relaxed">
          {analysis.trendDescription}
        </p>
      </div>

      {/* Dòng khuyến cáo tham khảo bắt buộc theo SOW v6 */}
      <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50/70 p-3.5 text-xs text-amber-900 flex items-start gap-2.5">
        <span className="text-base leading-none">⚠️</span>
        <p className="leading-relaxed">
          <strong className="font-semibold">Lưu ý tham khảo: </strong>
          {analysis.disclaimer}
        </p>
      </div>
    </div>
  );
}
