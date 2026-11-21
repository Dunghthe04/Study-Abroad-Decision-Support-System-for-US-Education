"use client";

import { CalendarIcon, ClockIcon } from "lucide-react";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import { formatVnDate } from "@/lib/billing-api";
import type { Fetched } from "@/lib/use-fetched";
import type { SubscriptionSummaryDto } from "@/types/billing";

const clamp = (n: number) => Math.min(100, Math.max(0, n));

// [Hạn mức] Mỗi kỳ gói một thanh tiến độ + lượt miễn phí. Chỉ phần trăm, không bao giờ hiện số token.
export function QuotaCard({ sub }: { sub: Fetched<SubscriptionSummaryDto | null> }) {
  const s = sub.data;
  const cycles = s?.cycles ?? [];

  return (
    <div>
      <AccountSectionHeader
        title="Hạn mức"
        description="Lượt hỏi đáp AI còn lại của từng gói. Mỗi lần dùng được tính vào gói hết hạn sớm nhất trước."
      />

      {!s ? (
        sub.error ? (
          <Alert variant="destructive">
            <AlertDescription>{sub.error}</AlertDescription>
          </Alert>
        ) : (
          <div className="space-y-4">
            <Skeleton className="h-24 w-full rounded-xl" />
            <Skeleton className="h-24 w-full rounded-xl" />
          </div>
        )
      ) : (
        <div className="space-y-8">
          {cycles.length > 0 && (
            <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
              {cycles.map((c) => (
                <li key={c.id} className="space-y-3 px-5 py-4 sm:px-6">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-semibold text-ink">{c.planName}</span>
                      {c.role === "member" && (
                        <span className="text-xs text-ink-2">chia sẻ từ {c.ownerName ?? "chủ gói"}</span>
                      )}
                      {c.exhausted && <Badge variant="risk">Đã dùng hết</Badge>}
                    </div>
                    <span className="text-sm font-medium text-ink tabular-nums">{c.usedPercent}% đã dùng</span>
                  </div>
                  <Progress value={clamp(c.usedPercent)} aria-label={`${c.planName}: đã dùng ${c.usedPercent}%`} />
                  <div className="flex flex-wrap gap-x-6 gap-y-1.5 text-xs text-ink-2">
                    {c.quotaResetsAt && (
                      <span className="flex items-center gap-1.5">
                        <CalendarIcon className="size-3.5 shrink-0 text-ink-3" aria-hidden />
                        Làm mới ngày {formatVnDate(c.quotaResetsAt)}
                      </span>
                    )}
                    <span className="flex items-center gap-1.5">
                      <ClockIcon className="size-3.5 shrink-0 text-ink-3" aria-hidden />
                      Hết hạn ngày {formatVnDate(c.endsAt)}
                    </span>
                  </div>
                </li>
              ))}
            </ul>
          )}

          <div className="space-y-4 rounded-xl border border-line bg-surface p-5 sm:p-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="space-y-1">
                <h2 className="font-heading text-lg font-semibold text-ink">
                  {s.free.quotaPeriodDays ? `Lượt miễn phí mỗi ${s.free.quotaPeriodDays} ngày` : "Lượt miễn phí hằng tháng"}
                </h2>
                <p className="text-xs text-ink-2">Dùng cho hỏi đáp kiến thức du học khi không còn gói trả phí.</p>
              </div>
              <span className="text-sm font-medium text-ink tabular-nums">{s.free.usedPercent}% đã dùng</span>
            </div>
            <Progress
              value={clamp(s.free.usedPercent)}
              aria-label={`${s.free.quotaPeriodDays ? `Lượt miễn phí (${s.free.quotaPeriodDays} ngày)` : "Lượt miễn phí tháng này"}: đã dùng ${s.free.usedPercent}%`}
            />
            <div className="flex items-center gap-2 border-t border-line pt-3.5 text-xs text-ink-2">
              <CalendarIcon className="size-3.5 shrink-0 text-ink-3" aria-hidden />
              <span>Làm mới ngày {formatVnDate(s.free.resetsAt)}</span>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
