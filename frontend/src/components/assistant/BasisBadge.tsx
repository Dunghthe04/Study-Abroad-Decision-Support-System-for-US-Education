"use client";

// Nhãn nói rõ lời giải thích hoặc kết quả dựa trên cái gì: hồ sơ thật hay giả định học sinh đang thử.
// Hiện ở đầu mỗi thẻ trong chat để không bao giờ nhầm hai thứ này với nhau.

import { cn } from "@/lib/utils";

interface BasisBadgeProps {
  kind: "real" | "simulated";
  /** Mô tả giả định ("GPA 3.6, SAT 1500"), chỉ dùng khi kind là simulated */
  label?: string | null;
}

export function BasisBadge({ kind, label }: BasisBadgeProps) {
  const simulated = kind === "simulated";
  return (
    <span
      className={cn(
        "inline-flex max-w-full items-center rounded-full px-2.5 py-0.5 text-[11px] font-semibold",
        simulated ? "bg-warn-wash text-warn" : "bg-brand/10 text-brand",
      )}
    >
      {simulated ? `Theo giả định bạn đang thử${label ? `: ${label}` : ""}` : "Theo hồ sơ thật của bạn"}
    </span>
  );
}
