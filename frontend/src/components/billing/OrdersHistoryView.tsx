"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CreditCardIcon } from "lucide-react";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyTitle,
} from "@/components/ui/empty";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { billingApi, formatVnd, formatVnDateTime, ORDER_STATUS_BADGE, ORDER_STATUS_LABEL } from "@/lib/billing-api";
import { useFetched } from "@/lib/use-fetched";
import { cn } from "@/lib/utils";

const PAGE_SIZE = 20;

// [UC-112] Lịch sử giao dịch của chính người dùng. Thứ tự mới nhất trước do máy chủ sắp.
export function OrdersHistoryView({ inAccount = false }: { inAccount?: boolean }) {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const orders = useFetched(`orders:${page}`, () => billingApi.listOrders(page, PAGE_SIZE));
  const data = orders.data;

  const header = inAccount ? (
    <AccountSectionHeader
      title="Lịch sử giao dịch"
      description="Danh sách các đơn đăng ký gói dịch vụ và trạng thái thanh toán của bạn."
      actions={
        data && data.items.length > 0 ? (
          <Link href="/plans" className={buttonVariants({ variant: "outline", size: "lg", className: "bg-surface" })}>
            <CreditCardIcon data-icon="inline-start" aria-hidden />
            Xem các gói
          </Link>
        ) : undefined
      }
    />
  ) : (
    <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
      <div className="space-y-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink">Lịch sử giao dịch</h1>
        <p className="text-sm text-ink-2">Danh sách các đơn đăng ký gói dịch vụ và trạng thái thanh toán của bạn.</p>
      </div>
      {data && data.items.length > 0 && (
        <Link href="/plans" className={buttonVariants({ variant: "outline", size: "lg", className: "bg-surface" })}>
          <CreditCardIcon data-icon="inline-start" aria-hidden />
          Xem các gói
        </Link>
      )}
    </div>
  );

  if (!data) {
    return (
      <div className="w-full">
        {header}
        {orders.error ? (
          <ErrorFallback title="Không tải được lịch sử giao dịch" description={orders.error} />
        ) : (
          <div className="space-y-3">
            <Skeleton className="h-12 w-full rounded-xl" />
            <Skeleton className="h-48 w-full rounded-xl" />
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="w-full">
      {header}

      {data.items.length === 0 ? (
        <Empty className="py-16">
          <EmptyHeader>
            <EmptyTitle className="text-base font-semibold text-ink">
              Bạn chưa có giao dịch nào
            </EmptyTitle>
            <EmptyDescription>
              Các đơn đăng ký gói USDSS Premium Cá nhân hoặc Gia đình sẽ hiển thị tại đây.
            </EmptyDescription>
          </EmptyHeader>
          <EmptyContent>
            <Link href="/plans" className={buttonVariants({ size: "lg" })}>
              <CreditCardIcon data-icon="inline-start" aria-hidden />
              Xem các gói
            </Link>
          </EmptyContent>
        </Empty>
      ) : (
        <div className={cn("space-y-4", orders.loading && "opacity-60")}>
          <div className="overflow-hidden rounded-xl border border-line bg-surface">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="pl-5 sm:pl-6">Mã đơn</TableHead>
                  <TableHead>Gói</TableHead>
                  <TableHead className="text-right">Số tiền</TableHead>
                  <TableHead>Thời điểm</TableHead>
                  <TableHead className="pr-5 sm:pr-6">Trạng thái</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((o) => (
                  <TableRow
                    key={o.orderCode}
                    className="cursor-pointer"
                    onClick={() => router.push(`/orders/${o.orderCode}`)}
                  >
                    <TableCell className="pl-5 font-mono sm:pl-6">
                      <Link
                        href={`/orders/${o.orderCode}`}
                        className="font-medium text-ink hover:text-brand hover:underline"
                        onClick={(e) => e.stopPropagation()}
                      >
                        {o.orderCode}
                      </Link>
                    </TableCell>
                    <TableCell className="font-medium text-ink">{o.planName}</TableCell>
                    <TableCell className="text-right font-medium text-ink tabular-nums">
                      {formatVnd(o.amountVnd)}
                    </TableCell>
                    <TableCell className="text-ink-2">{formatVnDateTime(o.createdAt)}</TableCell>
                    <TableCell className="pr-5 sm:pr-6">
                      <Badge variant={ORDER_STATUS_BADGE[o.status]}>
                        {ORDER_STATUS_LABEL[o.status]}
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <TablePagination
            page={page}
            totalPages={Math.max(1, data.totalPages)}
            totalCount={data.totalCount}
            itemLabel="giao dịch"
            disabled={orders.loading}
            onPageChange={setPage}
          />
        </div>
      )}
    </div>
  );
}
