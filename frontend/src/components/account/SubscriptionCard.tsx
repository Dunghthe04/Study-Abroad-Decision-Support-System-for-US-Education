"use client";

import Image from "next/image";
import Link from "next/link";
import { useState, type ReactNode } from "react";
import { PlusIcon } from "lucide-react";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import { OrdersHistoryView } from "@/components/billing/OrdersHistoryView";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { formatVnDate, formatVnDateTime } from "@/lib/billing-api";
import type { Fetched } from "@/lib/use-fetched";
import type { ExpiredCycleDto, SubscriptionCycleDto, SubscriptionSummaryDto } from "@/types/billing";

export const CYCLE_IMAGE = {
  personal: "/plans/personal_plan.png",
  family: "/plans/family_plan.png",
} as const;

export function cycleKindLabel(c: SubscriptionCycleDto): string {
  if (c.role === "member") return `Gia đình · Được chia sẻ bởi ${c.ownerName ?? "chủ gói"}`;
  return c.kind === "family" ? "Gia đình" : "Cá nhân";
}

// [Gói dịch vụ] Các gói đang dùng (mỗi lần mua một dòng) + lịch sử giao dịch. Hạn mức nằm ở mục "Hạn mức".
const PAGE = 3;

function CycleSection({ title, count, children }: { title: string; count: number; children: ReactNode }) {
  return (
    <div className="space-y-4">
      <h2 className="font-heading text-lg font-semibold text-ink">{title} ({count})</h2>
      <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">{children}</ul>
    </div>
  );
}

function CycleRow({ image, name, kind, purchasedAt, endsAt, badge }: {
  image: string; name: string; kind: string; purchasedAt: string; endsAt: string; badge?: ReactNode;
}) {
  return (
    <li className="flex flex-wrap items-center gap-x-6 gap-y-3 px-5 py-4 sm:px-6">
      <div className="flex min-w-0 flex-1 basis-56 items-center gap-3.5">
        <Image src={image} alt="" width={40} height={40} className="size-10 shrink-0 object-contain" />
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="truncate font-semibold text-ink">{name}</span>
            {badge}
          </div>
          <p className="text-xs text-ink-2">{kind}</p>
        </div>
      </div>
      <dl className="flex gap-x-8 text-sm">
        <div>
          <dt className="text-xs text-ink-3">Ngày mua</dt>
          <dd className="text-ink tabular-nums" title={formatVnDateTime(purchasedAt)}>{formatVnDate(purchasedAt)}</dd>
        </div>
        <div>
          <dt className="text-xs text-ink-3">Ngày hết hạn</dt>
          <dd className="text-ink tabular-nums">{formatVnDate(endsAt)}</dd>
        </div>
      </dl>
    </li>
  );
}

export function SubscriptionCard({ sub, expired }: { sub: Fetched<SubscriptionSummaryDto | null>; expired: Fetched<ExpiredCycleDto[]> }) {
  const s = sub.data;
  const cycles = s?.cycles ?? [];
  const expiredList = expired.data ?? [];   // lỗi tải thì không có dữ liệu: mục ẩn
  const [shown, setShown] = useState(PAGE);
  const moreCount = Math.min(PAGE, expiredList.length - shown);

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
        ) : (
          <>
            {cycles.length === 0 ? (
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
          <CycleSection title="Đang hoạt động" count={cycles.length}>
            {cycles.map((c) => (
              <CycleRow key={c.id} image={CYCLE_IMAGE[c.kind]} name={c.planName} kind={cycleKindLabel(c)}
                purchasedAt={c.purchasedAt} endsAt={c.endsAt} badge={c.expiringSoon ? <Badge variant="warn">Sắp hết hạn</Badge> : null} />
            ))}
          </CycleSection>
            )}
          </>
        )}

        {expiredList.length > 0 && (
          <CycleSection title="Đã hết hạn" count={expiredList.length}>
            {expiredList.slice(0, shown).map((c) => (
              <CycleRow key={c.id} image={CYCLE_IMAGE[c.kind]} name={c.planName} kind={c.kind === "family" ? "Gia đình" : "Cá nhân"}
                purchasedAt={c.purchasedAt} endsAt={c.endsAt} />
            ))}
            {moreCount > 0 && (
              <li className="px-5 py-3 sm:px-6">
                <Button variant="ghost" onClick={() => setShown((n) => n + PAGE)}>Hiện thêm {moreCount}</Button>
              </li>
            )}
          </CycleSection>
        )}

        <div className="space-y-4">
          <h2 className="font-heading text-lg font-semibold text-ink">Lịch sử giao dịch</h2>
          <OrdersHistoryView inAccount />
        </div>
      </div>
    </div>
  );
}
