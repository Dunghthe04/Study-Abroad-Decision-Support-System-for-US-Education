import type { AcademicAnalysisResponse } from "@/types/academic";

interface GpaSummaryCardProps {
  analysis: AcademicAnalysisResponse;
}

/**
 * [USAS-365] Executive Summary: Tổng quan năng lực học thuật và GPA quy đổi chuẩn WES.
 * - Fix C365-04: Xử lý đúng khi totalTerms < 2 (không gán nhãn Phong độ ổn định khi thiếu dữ liệu).
 * - Fix C365-05: Bỏ mẫu số / 4.0 cố định cho Weighted GPA (hỗ trợ thang nâng cao tới 4.5).
 * - Cải thiện độ tương phản disclaimer và câu từ quy đổi tham khảo.
 */
export function GpaSummaryCard({ analysis }: GpaSummaryCardProps) {
  const isUpward = analysis.trend === "upward";
  const isDownward = analysis.trend === "downward";
  const isInsufficientTerms = analysis.totalTerms < 2;

  const trendStatus = isInsufficientTerms
    ? {
        label: "Chưa đủ dữ liệu xu hướng",
        color: "text-slate-700 bg-slate-100 border-slate-200",
      }
    : isUpward
    ? {
        label: "Xu hướng tiến bộ (Upward Trend)",
        color: "text-emerald-700 bg-emerald-50 border-emerald-200",
      }
    : isDownward
    ? {
        label: "Xu hướng giảm (Downward Trend)",
        color: "text-amber-800 bg-amber-50 border-amber-200",
      }
    : {
        label: "Phong độ ổn định (Consistent)",
        color: "text-slate-800 bg-slate-100 border-slate-200",
      };

  return (
    <div className="rounded-xl border border-slate-200/90 bg-white p-5 sm:p-7 shadow-xs">
      {/* Header bar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-100 pb-5">
        <div>
          <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
            Kết quả quy đổi học thuật
          </span>
          <h2 className="text-xl font-bold tracking-tight text-slate-900 mt-0.5">
            Chỉ số GPA & Năng lực học tập
          </h2>
          <p className="text-xs text-slate-600 mt-1">
            Quy đổi tham khảo theo {analysis.scaleSource}
          </p>
        </div>

        <div className="flex items-center gap-2">
          <div
            className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-semibold ${trendStatus.color}`}
          >
            <span className="h-1.5 w-1.5 rounded-full bg-current" />
            <span>{trendStatus.label}</span>
          </div>
        </div>
      </div>

      {/* Main KPI Row */}
      <div className="mt-6 grid gap-6 md:grid-cols-12 items-center">
        {/* Left: Primary Score Block (5 cols) */}
        <div className="md:col-span-5 flex items-baseline gap-4 border-b md:border-b-0 md:border-r border-slate-100 pb-5 md:pb-0 md:pr-6">
          <div>
            <span className="block text-xs font-semibold text-slate-600">
              GPA Thang 4.0 (WES Standard)
            </span>
            <div className="mt-1 flex items-baseline gap-1.5">
              <span className="text-4xl font-extrabold tracking-tight text-slate-900">
                {analysis.unweightedGpa.toFixed(2)}
              </span>
              <span className="text-sm font-medium text-slate-500">/ 4.00</span>
            </div>
            <span className="mt-1 block text-xs text-slate-500">
              Unweighted (Không trọng số)
            </span>
          </div>

          <div className="space-y-2 border-l border-slate-200/80 pl-4">
            <div>
              <span className="text-xs text-slate-500 block">Weighted GPA</span>
              <span className="text-base font-bold text-slate-900">
                {analysis.weightedGpa.toFixed(2)}
              </span>
              <span className="text-[11px] text-slate-500 block">
                (Tín chỉ / môn nâng cao)
              </span>
            </div>
            <div>
              <span className="text-xs text-slate-500 block">Điểm TB hệ 10</span>
              <span className="text-sm font-semibold text-slate-800">
                {analysis.rawAverage.toFixed(2)}
                <span className="text-xs text-slate-500 font-normal"> / 10.0</span>
              </span>
            </div>
          </div>
        </div>

        {/* Right: Strategic Insight (7 cols) */}
        <div className="md:col-span-7 space-y-2.5">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-800">
              Nhận xét xu hướng học tập:
            </span>
            <span className="text-xs font-medium text-slate-500">
              {analysis.totalSubjects} môn ({analysis.totalTerms} học kỳ)
            </span>
          </div>

          <p className="text-xs sm:text-sm text-slate-700 leading-relaxed bg-slate-50 rounded-lg p-3.5 border border-slate-100">
            {analysis.trendDescription}
          </p>

          <p className="text-xs text-slate-500 leading-normal">
            * {analysis.disclaimer}
          </p>
        </div>
      </div>
    </div>
  );
}
