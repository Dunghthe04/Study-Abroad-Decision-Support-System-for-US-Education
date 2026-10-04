import type { TermTrend } from "@/types/academic";

interface TermTrendChartProps {
  terms: TermTrend[];
}

/**
 * [USAS-365] Biểu đồ xu hướng học tập qua các kỳ học (3 năm THPT/ĐH):
 * Thể hiện đà tăng trưởng (Growth Mindset) mà ban tuyển sinh đại học Mỹ đặc biệt đánh giá cao.
 */
export function TermTrendChart({ terms }: TermTrendChartProps) {
  if (terms.length === 0) {
    return null;
  }

  // Sắp xếp các kỳ theo thứ tự thời gian
  const sorted = [...terms].sort((a, b) => a.termOrder - b.termOrder);

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
      <div className="border-b border-slate-100 pb-4 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
        <div>
          <h3 className="text-lg font-bold text-slate-900">
            Biến Động Điểm Số Qua Các Học Kỳ (3 Năm Gần Nhất)
          </h3>
          <p className="text-xs text-slate-500 mt-1">
            Biểu đồ xu hướng GPA thang 4.0 và điểm hệ 10 qua từng giai đoạn học tập.
          </p>
        </div>
        <div className="flex items-center gap-4 text-xs">
          <div className="flex items-center gap-1.5">
            <span className="h-3 w-3 rounded-full bg-blue-600 inline-block" />
            <span className="text-slate-600 font-medium">GPA Thang 4.0</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="h-3 w-3 rounded-full bg-slate-300 inline-block" />
            <span className="text-slate-600 font-medium">Hệ 10 (thao chiếu)</span>
          </div>
        </div>
      </div>

      <div className="mt-6">
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-6">
          {sorted.map((item, idx) => {
            // Chiều cao tương đối trên thang 4.0 (0 đến 4.0)
            const heightPercent = Math.max(15, Math.min(100, Math.round((item.gpa4 / 4.0) * 100)));

            return (
              <div
                key={item.termOrder}
                className="flex flex-col items-center justify-end rounded-lg border border-slate-100 bg-slate-50/60 p-3"
              >
                {/* Thông số điểm */}
                <div className="text-center mb-3">
                  <span className="text-base font-extrabold text-blue-600">
                    {item.gpa4.toFixed(2)}
                  </span>
                  <div className="text-[11px] text-slate-400">
                    ({item.rawAverage.toFixed(1)}/10)
                  </div>
                </div>

                {/* Thanh cột trực quan */}
                <div className="h-32 w-10 flex items-end justify-center rounded-lg bg-slate-200/70 p-1">
                  <div
                    className="w-full rounded-md bg-blue-600 transition-all duration-700 hover:bg-blue-700"
                    style={{ height: `${heightPercent}%` }}
                    title={`${item.termName}: GPA ${item.gpa4.toFixed(2)} (Hệ 10: ${item.rawAverage})`}
                  />
                </div>

                {/* Nhãn kỳ */}
                <div className="mt-3 text-center">
                  <div className="text-xs font-semibold text-slate-800">
                    {item.termName}
                  </div>
                  <div className="text-[10px] text-slate-400">
                    {item.subjectCount} môn
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
