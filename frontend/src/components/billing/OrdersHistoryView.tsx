"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { billingApi, formatVnd, formatVnDateTime, ORDER_STATUS_BADGE, ORDER_STATUS_LABEL } from "@/lib/billing-api";
import { useFetched } from "@/lib/use-fetched";
import { cn } from "@/lib/utils";

const PAGE_SIZE = 20;

// [UC-112] Lịch sử giao dịch của chính người dùng. Thứ tự mới nhất trước do máy chủ sắp.
export function OrdersHistoryView() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const orders = useFetched(`orders:${page}`, () => billingApi.listOrders(page, PAGE_SIZE));
  const data = orders.data;

  if (!data) {
    return orders.error ? (
      <ErrorFallback title="Không tải được lịch sử giao dịch" description={orders.error} />
    ) : (
      <div className="flex min-h-[40vh] items-center justify-center">
        <Spinner className="size-8 text-brand" />
      </div>
    );
  }

  return (
    <div className="w-full space-y-4">
      <h1 className="text-h1">Lịch sử giao dịch</h1>
      <Card>
        <CardContent className={cn("space-y-4", orders.loading && "opacity-60")}>
          {data.items.length === 0 ? (
            <div className="space-y-3 py-6 text-center text-sm text-muted-foreground">
              <p>Bạn chưa có giao dịch nào.</p>
              <Link href="/plans" className={buttonVariants({ size: "sm" })}>
                Xem các gói
              </Link>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Mã đơn</TableHead>
                  <TableHead>Gói</TableHead>
                  <TableHead className="text-right">Số tiền</TableHead>
                  <TableHead>Thời điểm</TableHead>
                  <TableHead>Trạng thái</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((o) => (
                  <TableRow key={o.orderCode} className="cursor-pointer" onClick={() => router.push(`/orders/${o.orderCode}`)}>
                    <TableCell className="font-mono">
                      <Link href={`/orders/${o.orderCode}`} className="hover:underline" onClick={(e) => e.stopPropagation()}>
                        {o.orderCode}
                      </Link>
                    </TableCell>
                    <TableCell>{o.planName}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatVnd(o.amountVnd)}</TableCell>
                    <TableCell>{formatVnDateTime(o.createdAt)}</TableCell>
                    <TableCell>
                      <Badge variant={ORDER_STATUS_BADGE[o.status]}>
                        {ORDER_STATUS_LABEL[o.status]}
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
          <TablePagination
            page={page}
            totalPages={Math.max(1, data.totalPages)}
            totalCount={data.totalCount}
            itemLabel="giao dịch"
            disabled={orders.loading}
            onPageChange={setPage}
          />
        </CardContent>
      </Card>
    </div>
  );
}
