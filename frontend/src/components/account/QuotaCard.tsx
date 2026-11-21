"use client";

import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { formatVnDate } from "@/lib/billing-api";
import type { Fetched } from "@/lib/use-fetched";
import type { SubscriptionSummaryDto } from "@/types/billing";

const clamp = (n: number) => Math.min(100, Math.max(0, n));

function UsageBar({ percent, label }: { percent: number; label: string }) {
  const v = clamp(percent);
  return (
    <div role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={v} aria-label={label} className="h-3 w-full overflow-hidden rounded-full bg-muted">
      <div className="h-full rounded-full bg-brand transition-[width]" style={{ width: `${v}%` }} />
    </div>
  );
}

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
            <section className="space-y-3">
              <h2 className="font-heading text-lg font-semibold text-ink">Gói trả phí</h2>
            <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
              {cycles.map((c) => (
                <li key={c.id} className="space-y-4 px-5 py-5 sm:px-6">
                  <div className="flex items-start justify-between gap-4">
                    <div className="space-y-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-base font-semibold text-ink">{c.planName}</span>
                        {c.exhausted && <Badge variant="risk">Đã dùng hết</Badge>}
                      </div>
                      <p className="text-sm text-ink-2">
                        {c.role === "member" && <>Chia sẻ từ {c.ownerName ?? "chủ gói"} · </>}
                        {c.quotaResetsAt
                          ? `Đặt lại vào ngày ${formatVnDate(c.quotaResetsAt)}`
                          : `Hết hạn vào ngày ${formatVnDate(c.endsAt)}`}
                      </p>
                    </div>
                    <span className="shrink-0 text-sm text-ink-2 tabular-nums">{c.usedPercent}% đã sử dụng</span>
                  </div>
                  <UsageBar percent={c.usedPercent} label={`${c.planName}: đã dùng ${c.usedPercent}%`} />
                </li>
              ))}
            </ul>
            </section>
          )}

          <section className="space-y-3">
            <h2 className="font-heading text-lg font-semibold text-ink">Miễn phí</h2>
          <div className="space-y-4 rounded-xl border border-line bg-surface px-5 py-5 sm:px-6">
            <div className="flex items-start justify-between gap-4">
              <div className="space-y-1">
                <h3 className="text-base font-semibold text-ink">
                  {s.free.quotaPeriodDays ? `Miễn phí mỗi ${s.free.quotaPeriodDays} ngày` : "Miễn phí hàng tháng"}
                </h3>
                <p className="text-sm text-ink-2">Đặt lại vào ngày {formatVnDate(s.free.resetsAt)}</p>
              </div>
              <span className="shrink-0 text-sm text-ink-2 tabular-nums">{s.free.usedPercent}% đã sử dụng</span>
            </div>
            <UsageBar percent={s.free.usedPercent} label={`Miễn phí: đã dùng ${s.free.usedPercent}%`} />
          </div>
            <p className="text-xs text-ink-2">Hạn mức miễn phí chỉ hỗ trợ hỏi đáp các tính năng giống gói Miễn phí.</p>
          </section>
        </div>
      )}
    </div>
  );
}
