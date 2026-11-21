"use client";

import Link from "next/link";
import {
  CalendarIcon,
  ClockIcon,
  CrownIcon,
  ReceiptTextIcon,
  RefreshCwIcon,
  TriangleAlertIcon,
} from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import { billingApi, formatVnDate } from "@/lib/billing-api";
import { useFetched } from "@/lib/use-fetched";
import type { FamilyPoolDto, PoolDto } from "@/types/billing";

// [Gói dịch vụ] Chỉ phần trăm, không bao giờ hiện số token (billing-plan-v2 "Percent-only usage").
function PoolPanel({ title, pool }: { title: string; pool: PoolDto | FamilyPoolDto }) {
  const member = "role" in pool && pool.role === "member" ? pool.ownerName : null;
  const clamped = Math.min(100, Math.max(0, pool.usedPercent));

  return (
    <div className="space-y-4 rounded-xl border border-line bg-surface p-5 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="font-heading text-lg font-semibold text-ink">{title}</h2>
            <Badge variant="brand">{pool.planName}</Badge>
            {pool.exhausted && <Badge variant="risk">Đã dùng hết</Badge>}
          </div>
          {member && (
            <p className="text-xs text-ink-2">Thành viên gói Gia đình của {member}</p>
          )}
        </div>

        <div className="text-left sm:text-right">
          <div className="text-2xl font-semibold text-ink tabular-nums">
            {pool.usedPercent}%
          </div>
          <div className="text-xs text-ink-3">Lượt hỏi kỳ này đã dùng</div>
        </div>
      </div>

      <Progress
        value={clamped}
        aria-label={`Lượt hỏi của kỳ này: đã dùng ${pool.usedPercent}%`}
      />

      {(pool.quotaResetsAt || pool.endsAt || pool.queued) && (
        <div className="grid grid-cols-1 gap-2.5 border-t border-line pt-3.5 text-xs text-ink-2 sm:grid-cols-2">
          {pool.quotaResetsAt && (
            <div className="flex items-center gap-2">
              <RefreshCwIcon className="size-3.5 shrink-0 text-ink-3" aria-hidden />
              <span>
                Làm mới hạn mức ngày {formatVnDate(pool.quotaResetsAt)}
                {pool.quotaPeriodDays ? ` (mỗi ${pool.quotaPeriodDays} ngày)` : ""}
              </span>
            </div>
          )}
          {pool.endsAt && (
            <div className="flex items-center gap-2">
              <CalendarIcon className="size-3.5 shrink-0 text-ink-3" aria-hidden />
              <span>Hết hạn ngày {formatVnDate(pool.endsAt)}</span>
            </div>
          )}
          {pool.queued && (
            <div className="flex items-center gap-2 sm:col-span-2">
              <ClockIcon className="size-3.5 shrink-0 text-ink-3" aria-hidden />
              <span>
                {pool.queued.planName}: kỳ tiếp theo bắt đầu ngày{" "}
                {formatVnDate(pool.queued.expectedStartsAt ?? pool.endsAt)} hoặc khi dùng hết kỳ này
              </span>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export function SubscriptionCard() {
  const { user } = useAuth();
  const sub = useFetched(`sub:${user?.id}`, billingApi.subscription);
  const s = sub.data;

  const tierBadge = s ? (
    s.tier === "premium" ? (
      <Badge variant="brand">USDSS Premium</Badge>
    ) : (
      <Badge variant="neutral">Miễn phí</Badge>
    )
  ) : undefined;

  return (
    <div>
      <AccountSectionHeader
        title="Gói dịch vụ"
        description="Theo dõi hạn mức hỏi đáp AI trong kỳ hiện tại và quản lý gói đăng ký của bạn."
        badge={tierBadge}
        actions={
          <>
            <Link href="/orders" className={buttonVariants({ variant: "outline", size: "lg", className: "bg-surface" })}>
              <ReceiptTextIcon data-icon="inline-start" aria-hidden />
              Lịch sử giao dịch
            </Link>
            <Link href="/plans" className={buttonVariants({ size: "lg" })}>
              <CrownIcon data-icon="inline-start" aria-hidden />
              Xem các gói
            </Link>
          </>
        }
      />

      <div className="space-y-4">
        {!s ? (
          sub.error ? (
            <Alert variant="destructive">
              <AlertDescription>{sub.error}</AlertDescription>
            </Alert>
          ) : (
            <div className="space-y-4">
              <Skeleton className="h-36 w-full rounded-xl" />
              <Skeleton className="h-32 w-full rounded-xl" />
            </div>
          )
        ) : (
          <>
            {(s.lowQuota || s.expiringSoon) && (
              <Alert variant="warning">
                <TriangleAlertIcon aria-hidden />
                <AlertDescription className="flex flex-wrap items-center justify-between gap-2">
                  <span>
                    {s.lowQuota && "Bạn đã dùng hơn 80% lượt hỏi của gói đang dùng. "}
                    {s.expiringSoon && "Gói của bạn sắp hết hạn."}
                  </span>
                  <Link href="/plans" className="font-semibold underline underline-offset-4">
                    Xem gói
                  </Link>
                </AlertDescription>
              </Alert>
            )}

            {s.personal && <PoolPanel title="Cá nhân" pool={s.personal} />}
            {s.family && <PoolPanel title="Gia đình" pool={s.family} />}

            <div className="space-y-4 rounded-xl border border-line bg-surface p-5 sm:p-6">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="space-y-1">
                  <h2 className="font-heading text-lg font-semibold text-ink">
                    {s.free.quotaPeriodDays ? `Lượt miễn phí mỗi ${s.free.quotaPeriodDays} ngày` : "Lượt miễn phí hằng tháng"}
                  </h2>
                  <p className="text-xs text-ink-2">
                    Dùng cho hỏi đáp kiến thức du học khi không còn gói trả phí.
                  </p>
                </div>

                <div className="text-left sm:text-right">
                  <div className="text-2xl font-semibold text-ink tabular-nums">
                    {s.free.usedPercent}%
                  </div>
                  <div className="text-xs text-ink-3">
                    {s.free.quotaPeriodDays ? `Chu kỳ ${s.free.quotaPeriodDays} ngày` : "Tháng này"}
                  </div>
                </div>
              </div>

              <Progress
                value={Math.min(100, Math.max(0, s.free.usedPercent))}
                aria-label={`${s.free.quotaPeriodDays ? `Lượt miễn phí (${s.free.quotaPeriodDays} ngày)` : "Lượt miễn phí tháng này"}: đã dùng ${s.free.usedPercent}%`}
              />

              <div className="flex items-center gap-2 border-t border-line pt-3.5 text-xs text-ink-2">
                <RefreshCwIcon className="size-3.5 shrink-0 text-ink-3" aria-hidden />
                <span>Làm mới ngày {formatVnDate(s.free.resetsAt)}</span>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
