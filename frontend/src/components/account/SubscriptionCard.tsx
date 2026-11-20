"use client";

import Link from "next/link";
import { ReceiptTextIcon, TriangleAlertIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Spinner } from "@/components/ui/spinner";
import { billingApi, formatVnDate } from "@/lib/billing-api";
import { useFetched } from "@/lib/use-fetched";
import type { FamilyPoolDto, PoolDto } from "@/types/billing";

// [Gói dịch vụ] Chỉ phần trăm, không bao giờ hiện số token (billing-plan-v2 "Percent-only usage").
function UsageBar({ label, percent }: { label: string; percent: number }) {
  return (
    <div className="space-y-1">
      <div className="flex justify-between text-xs text-muted-foreground">
        <span>{label}</span>
        <span className="tabular-nums">Đã dùng {percent}%</span>
      </div>
      <Progress value={Math.min(100, Math.max(0, percent))} aria-label={`${label}: đã dùng ${percent}%`} />
    </div>
  );
}

function PoolRow({ title, pool }: { title: string; pool: PoolDto | FamilyPoolDto }) {
  const member = "role" in pool && pool.role === "member" ? pool.ownerName : null;
  return (
    <div className="space-y-2 rounded-lg border border-line p-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-semibold">{title}</span>
        <span className="text-sm text-muted-foreground">{pool.planName}</span>
        {pool.exhausted && <Badge variant="risk">Đã dùng hết</Badge>}
      </div>
      {member && <p className="text-xs text-muted-foreground">Thành viên gói Gia đình của {member}</p>}
      <UsageBar label="Lượt hỏi của kỳ này" percent={pool.usedPercent} />
      {pool.endsAt && <p className="text-xs text-muted-foreground">Hết hạn ngày {formatVnDate(pool.endsAt)}</p>}
      {pool.queued && (
        <p className="text-xs text-muted-foreground">
          {pool.queued.planName}: kỳ tiếp theo bắt đầu ngày{" "}
          {formatVnDate(pool.queued.expectedStartsAt ?? pool.endsAt)} hoặc khi dùng hết kỳ này
        </p>
      )}
    </div>
  );
}

export function SubscriptionCard() {
  const { user } = useAuth();
  const sub = useFetched(`sub:${user?.id}`, billingApi.subscription);
  const s = sub.data;

  return (
    <Card>
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
        <CardTitle className="flex items-center gap-2">
          Gói dịch vụ
          {s && (s.tier === "premium" ? <Badge variant="brand">USDSS Premium</Badge> : <Badge variant="neutral">Miễn phí</Badge>)}
        </CardTitle>
        <div className="flex flex-wrap gap-2">
          <Link href="/orders" className={buttonVariants({ variant: "outline", size: "sm" })}>
            <ReceiptTextIcon />
            Lịch sử giao dịch
          </Link>
          <Link href="/plans" className={buttonVariants({ size: "sm" })}>
            Xem các gói
          </Link>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        {!s ? (
          sub.error ? (
            <p className="text-sm text-risk">{sub.error}</p>
          ) : (
            <Spinner className="text-brand" />
          )
        ) : (
          <>
            {(s.lowQuota || s.expiringSoon) && (
              <Alert variant="warning">
                <TriangleAlertIcon />
                <AlertDescription className="flex flex-wrap items-center justify-between gap-2">
                  <span>
                    {s.lowQuota && "Bạn đã dùng hơn 80% lượt hỏi của gói đang dùng. "}
                    {s.expiringSoon && "Gói của bạn sắp hết hạn."}
                  </span>
                  <Link href="/plans" className="font-medium underline">
                    Xem gói
                  </Link>
                </AlertDescription>
              </Alert>
            )}
            {s.personal && <PoolRow title="Cá nhân" pool={s.personal} />}
            {s.family && <PoolRow title="Gia đình" pool={s.family} />}
            <div className="space-y-2 rounded-lg border border-line p-3">
              <span className="font-semibold">Lượt miễn phí hằng tháng</span>
              <p className="text-xs text-muted-foreground">Dùng cho hỏi đáp kiến thức du học khi không còn gói trả phí.</p>
              <UsageBar label="Lượt miễn phí tháng này" percent={s.free.usedPercent} />
              <p className="text-xs text-muted-foreground">Làm mới ngày {formatVnDate(s.free.resetsAt)}</p>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}
