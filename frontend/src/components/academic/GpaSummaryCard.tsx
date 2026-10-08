import type { AcademicAnalysisResponse } from "@/types/academic";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

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
        variant: "neutral" as const,
      }
    : isUpward
    ? {
        label: "Xu hướng tiến bộ (Upward Trend)",
        variant: "ok" as const,
      }
    : isDownward
    ? {
        label: "Xu hướng giảm (Downward Trend)",
        variant: "risk" as const,
      }
    : {
        label: "Phong độ ổn định (Consistent)",
        variant: "neutral" as const,
      };

  return (
    <Card>
      {/* Header bar */}
      <CardHeader>
        <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
          <div>
            <span className="text-label">
              Kết quả quy đổi học thuật
            </span>
            <CardTitle>
              <h2>Chỉ số GPA & Năng lực học tập</h2>
            </CardTitle>
            <CardDescription>
              Quy đổi tham khảo theo {analysis.scaleSource}
            </CardDescription>
          </div>

          <Badge variant={trendStatus.variant}>
            {trendStatus.label}
          </Badge>
        </div>
      </CardHeader>

      {/* Main KPI Row */}
      <CardContent className="grid items-center gap-6 md:grid-cols-12">
        {/* Left: Primary Score Block (5 cols) */}
        <div className="flex items-baseline gap-4 md:col-span-5">
          <div>
            <span className="block text-label">
              GPA Thang 4.0 (WES Standard)
            </span>
            <div className="mt-1 flex items-baseline gap-1.5">
              <span className="text-numeric text-4xl">
                {analysis.unweightedGpa.toFixed(2)}
              </span>
              <span className="text-muted-foreground">/ 4.00</span>
            </div>
            <span className="mt-1 block text-xs text-muted-foreground">
              Unweighted (Không trọng số)
            </span>
          </div>

          <div className="space-y-2">
            <div>
              <span className="block text-xs text-muted-foreground">Weighted GPA</span>
              <span className="text-numeric text-base">
                {analysis.weightedGpa.toFixed(2)}
              </span>
              <span className="block text-xs text-muted-foreground">
                (Tín chỉ / môn nâng cao)
              </span>
            </div>
            <div>
              <span className="block text-xs text-muted-foreground">Điểm TB hệ 10</span>
              <span className="text-numeric">
                {analysis.rawAverage.toFixed(2)}
                <span className="text-xs font-normal text-muted-foreground"> / 10.0</span>
              </span>
            </div>
          </div>
        </div>

        {/* Right: Strategic Insight (7 cols) */}
        <div className="space-y-2.5 md:col-span-7">
          <div className="flex items-center justify-between">
            <span className="font-medium">
              Nhận xét xu hướng học tập:
            </span>
            <span className="text-xs text-muted-foreground">
              {analysis.totalSubjects} đầu điểm ({analysis.totalTerms} học kỳ)
            </span>
          </div>

          <p>
            {analysis.trendDescription}
          </p>

          <p className="text-body-s">
            * {analysis.disclaimer}
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
