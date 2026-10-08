import type { SubjectGroupScore } from "@/types/academic";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";

interface SubjectGroupBreakdownProps {
  groups: SubjectGroupScore[];
}

/**
 * [USAS-365] Phân tích điểm trung bình theo từng nhóm môn học:
 * - Điều chỉnh tên chính xác theo dữ liệu: "Điểm Trung Bình Theo Nhóm Môn".
 * - Tối ưu kích cỡ chữ và độ tương phản dễ đọc cho học sinh và phụ huynh.
 */
export function SubjectGroupBreakdown({ groups }: SubjectGroupBreakdownProps) {
  if (groups.length === 0) {
    return null;
  }

  return (
    <Card className="h-full w-full min-w-0">
      <CardHeader>
        <CardTitle>
          <h3>Điểm Trung Bình Theo Nhóm Môn</h3>
        </CardTitle>
        <CardDescription>
          Thống kê điểm số và số lượng môn học theo từng nhóm lĩnh vực
        </CardDescription>
      </CardHeader>

      <CardContent className="flex-1">
        <div className="space-y-5">
          {groups.map((group) => {
            const percent = Math.min(100, Math.round((group.gpa4 / 4.0) * 100));

            return (
              <div
                key={group.groupKey}
                className="space-y-2"
              >
                <div className="flex flex-col justify-between gap-2 sm:flex-row sm:items-center">
                  <div className="flex min-w-0 flex-wrap items-center gap-1.5">
                    <span className="font-medium break-words">
                      {group.groupName}
                    </span>
                    <span className="shrink-0 text-xs text-muted-foreground">
                      {group.subjectsCount > group.subjects.length
                        ? `(${group.subjects.length} môn · ${group.subjectsCount} đầu điểm)`
                        : `(${group.subjects.length} môn)`}
                    </span>
                  </div>
                  <div className="flex shrink-0 items-baseline gap-1">
                    <span className="text-numeric">
                      {group.gpa4.toFixed(2)}
                    </span>
                    <span className="text-xs text-muted-foreground">/ 4.00</span>
                    <span className="text-xs text-muted-foreground">
                      ({group.rawAverage.toFixed(1)}/10)
                    </span>
                  </div>
                </div>

                {/* Thanh đo tỷ lệ điểm */}
                <Progress
                  value={percent}
                  aria-label={group.groupName}
                />

                {/* Danh sách tên môn */}
                <div className="flex flex-wrap gap-1.5">
                  {group.subjects.map((sub) => (
                    <Badge
                      key={sub}
                      variant="brand"
                    >
                      {sub}
                    </Badge>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      </CardContent>
    </Card>
  );
}
