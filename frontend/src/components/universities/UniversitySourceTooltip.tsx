"use client";

import type { ReactNode } from "react";
import { Badge } from "@/components/ui/badge";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { cn } from "cn";
import { universityDate } from "@/lib/university-display";
import type { UniversitySource } from "@/types/university";
import { SourceLink } from "./UniversityDetailParts";

const STATUS_LABELS: Record<string, string> = {
  verified: "Đã xác minh", unverified: "Chưa xác minh", derived: "Tính từ dữ liệu nguồn", not_published: "Không công bố",
};

export function findFieldSource(sources: UniversitySource[] | undefined, ...fieldNames: string[]): UniversitySource | undefined {
  if (!sources) return undefined;
  const applied = sources.filter((source) => source.appliedToSnapshot !== false && fieldNames.includes(source.fieldName));
  const verified = applied.find((source) => source.status === "verified");
  return verified ?? applied[0];
}

export function SourceTooltip({ source, children, className }: {
  source: UniversitySource | undefined;
  children: ReactNode;
  className?: string;
}) {
  if (!source) return <>{children}</>;
  const isVerified = source.status === "verified";
  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger
          delay={150}
          closeDelay={150}
          render={<span tabIndex={0} />}
          className={cn("cursor-help rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-ring", className)}
        >
          {children}
        </TooltipTrigger>
        <TooltipContent
          side="top"
          sideOffset={8}
          className="block w-72 max-w-[calc(100vw-2rem)] rounded-lg border border-line bg-popover p-3 text-left text-popover-foreground shadow-md [&>:last-child]:hidden"
        >
          <div className="space-y-2">
            <p className="text-xs text-muted-foreground">Nguồn thông tin</p>
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0 text-sm font-medium"><SourceLink url={source.sourceUrl} /></div>
              <Badge variant={isVerified ? "secondary" : "outline"} className={cn("shrink-0", isVerified && "border-transparent bg-ok-wash text-ok")}>
                {STATUS_LABELS[source.status] ?? "Chưa rõ trạng thái"}
              </Badge>
            </div>
            <p className="text-xs text-muted-foreground">Ngày lấy dữ liệu: {universityDate(source.retrievedAt)}</p>
            {source.dataYear && <p className="text-xs text-muted-foreground">Kỳ dữ liệu: {source.dataYear}</p>}
          </div>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
