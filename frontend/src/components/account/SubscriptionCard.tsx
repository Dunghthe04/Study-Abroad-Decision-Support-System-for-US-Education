"use client";

import Image from "next/image";
import Link from "next/link";
import { PlusIcon } from "lucide-react";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import { OrdersHistoryView } from "@/components/billing/OrdersHistoryView";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { formatVnDate, formatVnDateTime } from "@/lib/billing-api";
import type { Fetched } from "@/lib/use-fetched";
import type { SubscriptionCycleDto, SubscriptionSummaryDto } from "@/types/billing";

export const CYCLE_IMAGE = {
  personal: "/plans/personal_plan.png",
  family: "/plans/family_plan.png",
} as const;

export function cycleKindLabel(c: SubscriptionCycleDto): string {
  if (c.role === "member") return `Gia đình · Được chia sẻ bởi ${c.ownerName ?? "chủ gói"}`;
  return c.kind === "family" ? "Gia đình" : "Cá nhân";
}

// [Gói dịch vụ] Các gói đang dùng (mỗi lần mua một dòng) + lịch sử giao dịch. Hạn mức nằm ở mục "Hạn mức".
export function SubscriptionCard({ sub }: { sub: Fetched<SubscriptionSummaryDto | null> }) {
  const s = sub.data;
  const cycles = s?.cycles ?? [];

  return (
    <div>
      <AccountSectionHeader
        title="Gói dịch vụ"
        description="Các gói bạn đang dùng. Mua thêm gói để cộng dồn hạn mức."
        actions={
          <Link href="/plans" className={buttonVariants({ size: "lg" })}>
            <PlusIcon data-icon="inline-start" aria-hidden />
            Mua thêm gói
          </Link>
        }
      />

      <div className="space-y-8">
        {!s ? (
          sub.error ? (
            <Alert variant="destructive">
              <AlertDescription>{sub.error}</AlertDescription>
            </Alert>
          ) : (
            <Skeleton className="h-40 w-full rounded-xl" />
          )
        ) : cycles.length === 0 ? (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-line bg-surface px-5 py-4 sm:px-6">
            <div className="space-y-0.5">
              <p className="font-semibold text-ink">Bạn đang dùng gói Miễn phí</p>
              <p className="text-sm text-ink-2">Mua gói để mở khóa trợ lý AI và tăng hạn mức.</p>
            </div>
            <Link href="/plans" className={buttonVariants({ variant: "outline", className: "bg-surface" })}>
              Xem các gói
            </Link>
          </div>
        ) : (
          <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
            {cycles.map((c) => (
              <li key={c.id} className="flex flex-wrap items-center gap-x-6 gap-y-3 px-5 py-4 sm:px-6">
                <div className="flex min-w-0 flex-1 basis-56 items-center gap-3.5">
                  <Image src={CYCLE_IMAGE[c.kind]} alt="" width={40} height={40} className="size-10 shrink-0 object-contain" />
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="truncate font-semibold text-ink">{c.planName}</span>
                      {c.expiringSoon && <Badge variant="warn">Sắp hết hạn</Badge>}
                    </div>
                    <p className="text-xs text-ink-2">{cycleKindLabel(c)}</p>
                  </div>
                </div>
                <dl className="flex gap-x-8 text-sm">
                  <div>
                    <dt className="text-xs text-ink-3">Ngày mua</dt>
                    <dd className="text-ink tabular-nums" title={formatVnDateTime(c.purchasedAt)}>{formatVnDate(c.purchasedAt)}</dd>
                  </div>
                  <div>
                    <dt className="text-xs text-ink-3">Ngày hết hạn</dt>
                    <dd className="text-ink tabular-nums">{formatVnDate(c.endsAt)}</dd>
                  </div>
                </dl>
              </li>
            ))}
          </ul>
        )}

        <div className="space-y-4">
          <h2 className="font-heading text-lg font-semibold text-ink">Lịch sử giao dịch</h2>
          <OrdersHistoryView inAccount />
        </div>
      </div>
    </div>
  );
}
