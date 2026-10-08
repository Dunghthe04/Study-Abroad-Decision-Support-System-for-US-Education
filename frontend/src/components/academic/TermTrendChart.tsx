import type { TermTrend } from "@/types/academic";

interface TermTrendChartProps {
  terms: TermTrend[];
}

function formatTermLabel(termName: string) {
  // Hiển thị nhãn kỳ thành 2 dòng rõ ràng trên mobile, không bị truncate cắt cụt thành "N1 H..."
  // Ví dụ: "Năm 1 HK1" -> "Năm 1" (dòng 1) & "HK1" (dòng 2)
  // "Lớp 10 HK2" -> "Lớp 10" (dòng 1) & "HK2" (dòng 2)
  // "Học kỳ 1" -> "Học kỳ" (dòng 1) & "1" (dòng 2)
  const trimmed = termName.trim();
  const parts = trimmed.split(/\s+/);
  if (parts.length >= 2) {
    const last = parts[parts.length - 1];
    const prefix = parts.slice(0, parts.length - 1).join(" ");
    return (
      <div className="flex flex-col items-center leading-tight">
        <span className="text-slate-500 whitespace-nowrap">{prefix}</span>
        <span className="font-semibold text-slate-700 whitespace-nowrap">{last}</span>
      </div>
    );
  }
  return <span className="leading-tight break-words">{trimmed}</span>;
}

/**
 * [USAS-365] Biểu đồ xu hướng học tập qua các kỳ (Academic Trajectory):
 * - Fix C365-01: min-w-0 và overflow-x-auto tránh tràn màn hình mobile (375px/768px).
 * - Fix C365-09: Heading/tên kỳ không bị cắt cụt; hiển thị nhãn kỳ 2 dòng và min-width đủ trong vùng cuộn.
 * - Fix C365-10: Biểu diễn đúng tỷ lệ GPA theo giá trị thật (0 -> 0%), tách text và đường baseline rõ ràng.
 */
export function TermTrendChart({ terms }: TermTrendChartProps) {
  if (terms.length === 0) {
    return null;
  }

  const sorted = [...terms].sort((a, b) => a.termOrder - b.termOrder);

  return (
    <div className="rounded-xl border border-slate-200/90 bg-white p-5 sm:p-6 shadow-xs h-full flex flex-col justify-between min-w-0 w-full overflow-hidden">
      <div className="min-w-0 w-full">
        {/* Heading bar: cho phép xuống dòng trên mobile để không bị cắt */}
        <div className="border-b border-slate-100 pb-3 flex flex-col sm:flex-row sm:items-center justify-between gap-1.5 sm:gap-2">
          <div className="min-w-0">
            <h3 className="text-base font-bold text-slate-900">
              Biến Động Điểm Số Theo Học Kỳ
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Đánh giá tính kiên trì và đà tăng trưởng qua các kỳ học
            </p>
          </div>
          <span className="text-[11px] font-medium text-slate-500 whitespace-nowrap shrink-0 self-start sm:self-auto">
            Thang 4.0 WES
          </span>
        </div>

        {/* Chart scroll container (ngăn tràn document trên màn hình nhỏ) */}
        <div className="mt-6 w-full overflow-x-auto pb-1">
          <div className="flex items-end gap-2 sm:gap-3 h-48 min-w-full pb-2 border-b-2 border-slate-300">
            {sorted.map((item) => {
              // Fix C365-10: Chiều cao theo tỷ lệ thật (0 -> 0%, 4.0 -> 100%), không ép sàn nhân tạo (2% hay 6%)
              const heightPercent = Math.min(100, Math.max(0, (item.gpa4 / 4.0) * 100));

              return (
                <div
                  key={item.termOrder}
                  className="flex-1 min-w-[56px] sm:min-w-[64px] flex flex-col items-center justify-end h-full group"
                >
                  {/* Điểm GPA hiển thị rõ ràng trên cột/baseline (kể cả 0.00) */}
                  <span className="text-[11px] font-bold text-slate-800 mb-1 group-hover:text-blue-600 transition-colors">
                    {item.gpa4.toFixed(2)}
                  </span>

                  {/* Khung cột biểu đồ với đường baseline đáy */}
                  <div className="w-full max-w-[36px] bg-slate-100 rounded-t flex items-end justify-center overflow-hidden h-32 relative">
                    {heightPercent > 0 && (
                      <div
                        className="w-full bg-slate-800 group-hover:bg-blue-600 transition-all duration-300 rounded-t"
                        style={{ height: `${heightPercent}%` }}
                        title={`${item.termName}: GPA ${item.gpa4.toFixed(2)} (Hệ 10: ${item.rawAverage.toFixed(1)})`}
                      />
                    )}
                  </div>

                  {/* Nhãn kỳ học: 2 dòng rõ ràng, phân biệt được mọi kỳ trên mobile (Fix C365-09) */}
                  <div
                    className="mt-2 text-center text-[10px] w-full px-0.5"
                    title={item.termName}
                  >
                    {formatTermLabel(item.termName)}
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Chú thích điểm khởi đầu và hiện tại */}
        <div className="mt-3 flex flex-col sm:flex-row sm:items-center justify-between gap-1 text-[11px] text-slate-500">
          <span>Khởi đầu: {sorted[0]?.termName} ({sorted[0]?.gpa4.toFixed(2)})</span>
          <span>Hiện tại: {sorted[sorted.length - 1]?.termName} ({sorted[sorted.length - 1]?.gpa4.toFixed(2)})</span>
        </div>
      </div>
    </div>
  );
}
