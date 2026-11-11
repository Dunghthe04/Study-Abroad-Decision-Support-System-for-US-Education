"use client";

// Nhãn nói rõ lời giải thích hoặc kết quả dựa trên cái gì: hồ sơ thật hay giả định học sinh đang thử.
// Hiện ở đầu mỗi thẻ trong chat để không bao giờ nhầm hai thứ này với nhau.
// Giả định dùng viền nét đứt, giống thẻ trường đang mô phỏng, nên ảnh chụp màn hình cũng không bị hiểu nhầm.

import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

interface BasisBadgeProps {
  kind: "real" | "simulated";
  /** Mô tả giả định ("GPA 3.6, SAT 1500"), chỉ dùng khi kind là simulated */
  label?: string | null;
}

export function BasisBadge({ kind, label }: BasisBadgeProps) {
  const simulated = kind === "simulated";
  return (
    <Badge
      variant={simulated ? "neutral" : "brand"}
      className={cn("h-auto max-w-full whitespace-normal py-0.5", simulated && "border-dashed border-ink-2 text-ink")}
    >
      {simulated ? `Giả định, chưa phải kết quả thật${label ? `: ${label}` : ""}` : "Theo hồ sơ thật của bạn"}
    </Badge>
  );
}
