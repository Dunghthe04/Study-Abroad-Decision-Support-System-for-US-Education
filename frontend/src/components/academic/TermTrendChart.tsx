import type { TermTrend } from "@/types/academic";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

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
      <div className="flex flex-col items-center">
        <span className="whitespace-nowrap text-muted-foreground">{prefix}</span>
        <span className="whitespace-nowrap font-medium">{last}</span>
      </div>
    );
  }
  return <span className="break-words">{trimmed}</span>;
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
    <Card className="h-full w-full min-w-0">
      {/* Heading bar: cho phép xuống dòng trên mobile để không bị cắt */}
      <CardHeader>
        <div className="flex flex-col justify-between gap-2 sm:flex-row sm:items-center">
          <div className="min-w-0">
            <CardTitle>
              <h3>Biến Động Điểm Số Theo Học Kỳ</h3>
            </CardTitle>
            <CardDescription>
              Đánh giá tính kiên trì và đà tăng trưởng qua các kỳ học
            </CardDescription>
          </div>
          <span className="shrink-0 text-label">
            Thang 4.0 WES
          </span>
        </div>
      </CardHeader>

      <CardContent className="min-w-0">

        {/* Chart scroll container (ngăn tràn document trên màn hình nhỏ) */}
        <div className="overflow-x-auto">
          <div className="flex h-48 items-end gap-3">
            {sorted.map((item) => {
              // Fix C365-10: Chiều cao theo tỷ lệ thật (0 -> 0%, 4.0 -> 100%), không ép sàn nhân tạo (2% hay 6%)
              const heightPercent = Math.min(100, Math.max(0, (item.gpa4 / 4.0) * 100));

              return (
                <div
                  key={item.termOrder}
                  className="flex h-full min-w-14 flex-1 flex-col items-center justify-end gap-1"
                >
                  {/* Điểm GPA hiển thị rõ ràng trên cột/baseline (kể cả 0.00) */}
                  <span className="text-numeric text-xs">
                    {item.gpa4.toFixed(2)}
                  </span>

                  {/* Khung cột biểu đồ với đường baseline đáy */}
                  <div className="flex h-32 w-full max-w-9 items-end">
                    {heightPercent > 0 && (
                      <div
                        className="w-full bg-primary"
                        style={{ height: `${heightPercent}%` }}
                        title={`${item.termName}: GPA ${item.gpa4.toFixed(2)} (Hệ 10: ${item.rawAverage.toFixed(1)})`}
                      />
                    )}
                  </div>

                  {/* Nhãn kỳ học: 2 dòng rõ ràng, phân biệt được mọi kỳ trên mobile (Fix C365-09) */}
                  <div
                    className="text-center text-xs"
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
        <div className="mt-3 flex flex-col justify-between gap-1 text-body-s sm:flex-row sm:items-center">
          <span>Khởi đầu: {sorted[0]?.termName} ({sorted[0]?.gpa4.toFixed(2)})</span>
          <span>Hiện tại: {sorted[sorted.length - 1]?.termName} ({sorted[sorted.length - 1]?.gpa4.toFixed(2)})</span>
        </div>
      </CardContent>
    </Card>
  );
}
